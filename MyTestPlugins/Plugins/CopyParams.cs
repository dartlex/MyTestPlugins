using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMepParameterCopier
{
    [Transaction(TransactionMode.Manual)]
    public class CopyParams : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Получаем доступ к текущему документу и приложению
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                // 1. Быстрый фильтр Revit: собираем системные семейства (трубы/воздуховоды) и загружаемые (арматура/оборудование)
                FilteredElementCollector initialCollector = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                    {
                        new ElementClassFilter(typeof(MEPCurve)),
                        new ElementClassFilter(typeof(FamilyInstance))
                    }));

                // 2. Медленный фильтр LINQ: проверяем реальное наличие коннекторов через ConnectorManager
                List<Element> elementsWithConnectors = initialCollector
                    .Where(el => HasActiveConnectors(el))
                    .ToList();

                if (elementsWithConnectors.Count == 0)
                {
                    TaskDialog.Show("Предупреждение", "В модели не найдено элементов с MEP-подключениями.");
                    return Result.Succeeded;
                }

                // 3. Открываем транзакцию для внесения изменений в параметры модели
                using (Transaction tx = new Transaction(doc, "Копирование MEP параметров в ADSK"))
                {
                    tx.Start();

                    int successCount = 0;

                    foreach (Element el in elementsWithConnectors)
                    {
                        try
                        {
                            bool isUpdated = false;

                            // Шаг А. Копируем "Имя системы" (например, Т11 1) -> "ADSK_Система_Имя"
                            Parameter sysNameParam = el.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                            Parameter adskNameParam = el.LookupParameter("ADSK_Система_Имя");

                            if (sysNameParam != null && adskNameParam != null && sysNameParam.HasValue)
                            {
                                adskNameParam.Set(sysNameParam.AsString());
                                isUpdated = true;
                            }

                            // Шаг Б. Копируем "Сокращение для системы" (Т11) -> "ADSK_Система_Сокращение"
                            if (el is MEPCurve mepCurve)
                            {
                                MEPSystem system = mepCurve.MEPSystem;
                                if (system != null)
                                {
                                    // Сокращение хранится в ТИПЕ системы, получаем тип по его Id
                                    Element systemType = doc.GetElement(system.GetTypeId());
                                    Parameter sysAbbreviation = systemType.get_Parameter(BuiltInParameter.RBS_SYSTEM_ABBREVIATION_PARAM);
                                    Parameter adskAbbrevParam = el.LookupParameter("ADSK_Система_Сокращение");

                                    if (sysAbbreviation != null && adskAbbrevParam != null && sysAbbreviation.HasValue)
                                    {
                                        adskAbbrevParam.Set(sysAbbreviation.AsString());
                                        isUpdated = true;
                                    }
                                }
                            }

                            // Шаг В. Копируем "Базовый уровень" (Этаж 01) -> "ADSK_Номер стояка" (или ваш аналог для этажа/маркировки)
                            Parameter levelParam = el.get_Parameter(BuiltInParameter.RBS_START_LEVEL_PARAM);
                            Parameter adskLevelParam = el.LookupParameter("ADSK_Номер стояка");

                            if (levelParam != null && adskLevelParam != null && levelParam.HasValue)
                            {
                                // Системный параметр уровня хранит ID элемента. Получаем сам элемент Уровня, чтобы взять его текстовое имя
                                Element levelEl = doc.GetElement(levelParam.AsElementId());
                                if (levelEl != null)
                                {
                                    adskLevelParam.Set(levelEl.Name);
                                    isUpdated = true;
                                }
                            }

                            if (isUpdated)
                            {
                                successCount++;
                            }
                        }
                        catch (Exception)
                        {
                            // Если на каком-то одном элементе произойдет сбой (например, параметр заблокирован),
                            // мы пропускаем его и идем дальше, чтобы не прерывать весь процесс
                            continue;
                        }
                    }

                    // Сохраняем изменения в базе данных Revit
                    tx.Commit();

                    TaskDialog.Show("Готово", $"Параметры успешно обновлены для {successCount} элементов из {elementsWithConnectors.Count} найденных.");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Передаем ошибку в интерфейс Revit в случае критического сбоя
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Вспомогательный метод для проверки наличия активных MEP-коннекторов у элемента
        /// </summary>
        private bool HasActiveConnectors(Element el)
        {
            ConnectorManager cm = null;

            if (el is MEPCurve mepCurve)
            {
                cm = mepCurve.ConnectorManager;
            }
            else if (el is FamilyInstance familyInstance)
            {
                if (familyInstance.MEPModel != null)
                {
                    cm = familyInstance.MEPModel.ConnectorManager;
                }
            }

            return cm != null && cm.Connectors != null && cm.Connectors.Size > 0;
        }
    }
}
