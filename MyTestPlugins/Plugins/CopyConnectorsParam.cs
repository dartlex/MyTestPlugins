using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using System.Text.RegularExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTestPlugins.Plugins
{
    [Transaction(TransactionMode.Manual)]
    public class CopyConnectorsParam : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            var fitingCollector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctFitting).WhereElementIsNotElementType().ToElements();
            foreach (Element element in fitingCollector)
            {
                if (element is FamilyInstance fitting)
                {
                    if (fitting.MEPModel?.ConnectorManager == null)
                    {
                        continue;
                    }
                    ConnectorSet connectors = fitting.MEPModel.ConnectorManager.Connectors;

                    foreach (Connector connector in connectors)
                    {
                        if (connector.IsConnected == true)
                        {

                        }
                    }
                }
            }
            return Result.Succeeded;
        }
    }
}

//using Autodesk.Revit.Attributes;
//using Autodesk.Revit.DB;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.DB.Mechanical;
//using System.Text.RegularExpressions;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace MyTestPlugins.Plugins
//{
//    [Transaction(TransactionMode.Manual)]
//    public class CopyConnectorsParam : IExternalCommand
//    {
//        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
//        {
//            UIDocument uidoc = commandData.Application.ActiveUIDocument;
//            Document doc = uidoc.Document;

//            // Собираем все экземпляры фитингов воздуховодов из модели
//            var fitingCollector = new FilteredElementCollector(doc)
//                .OfCategory(BuiltInCategory.OST_DuctFitting)
//                .WhereElementIsNotElementType()
//                .ToElements();

//            // Открываем транзакцию, так как в конце мы будем изменять параметры фитинга
//            using (Transaction trans = new Transaction(doc, "Запись толщины в фитинги"))
//            {
//                trans.Start();

//                foreach (Element element in fitingCollector)
//                {
//                    if (element is FamilyInstance fitting)
//                    {
//                        if (fitting.MEPModel?.ConnectorManager == null)
//                        {
//                            continue;
//                        }

//                        // Переменная для хранения максимальной толщины среди всех направлений этого фитинга
//                        double maxThickness = 0;

//                        ConnectorSet connectors = fitting.MEPModel.ConnectorManager.Connectors;

//                        foreach (Connector connector in connectors)
//                        {
//                            if (connector.IsConnected == true)
//                            {
//                                // Инициализируем список посещенных элементов для защиты от вечного цикла (рекурсии)
//                                // Сразу добавляем туда текущий фитинг, чтобы не вернуться на него
//                                HashSet<int> visited = new HashSet<int> { fitting.Id.IntegerValue };

//                                // Ищем воздуховод по этой ветке (метод сам прошагает через соседние фитинги, если нужно)
//                                Duct connectedDuct = FindDuct(connector, visited);

//                                // Если воздуховод успешно найден (напрямую или через цепочку фитингов)
//                                if (connectedDuct != null)
//                                {
//                                    // Получаем типоразмер воздуховода (DuctType) через его ID
//                                    Element ductType = doc.GetElement(connectedDuct.GetTypeId());
//                                    string ductTypeName = ductType.Name; // Например: "Воздуховод ... b=0.5, класс ..."

//                                    // Регулярное выражение ищет "b=", возможные пробелы, и само число (с точкой или запятой)
//                                    Match match = Regex.Match(ductTypeName, @"b\s*=\s*(\d+[.,]?\d*)");

//                                    if (match.Success)
//                                    {
//                                        // Забираем значение из первой группы (то, что в круглых скобках регулярки)
//                                        string thicknessStr = match.Groups[1].Value;

//                                        // Принудительно меняем точку на запятую для корректного парсинга в русской Windows
//                                        thicknessStr = thicknessStr.Replace('.', ',');

//                                        // Переводим строковое число в тип double
//                                        if (double.TryParse(thicknessStr, out double currentThickness))
//                                        {
//                                            // Если найденная толщина больше сохраненной ранее — обновляем максимум
//                                            if (currentThickness > maxThickness)
//                                            {
//                                                maxThickness = currentThickness;
//                                            }
//                                        }
//                                    }
//                                }
//                            }
//                        }

//                        // --- ФИНАЛЬНЫЙ ШАГ ЦИКЛА ДЛЯ ТЕКУЩЕГО ФИТИНГА ---
//                        // Если мы нашли хоть какую-то толщину (> 0), записываем её в параметр фитинга
//                        if (maxThickness > 0)
//                        {
//                            // НАПРИМЕР: Ищем параметр по имени (замените "Имя_Параметра" на ваш реальный параметр, куда писать толщину)
//                            Parameter p = fitting.LookupParameter("Имя_Параметра_Толщины");
//                            if (p != null && !p.IsReadOnly)
//                            {
//                                // Если параметр текстовый: p.Set(maxThickness.ToString());
//                                // Если параметр числовой:
//                                p.Set(maxThickness);
//                            }
//                        }
//                    }
//                }

//                // Закрываем транзакцию и сохраняем изменения в модели Revit
//                trans.Commit();
//            }

//            return Result.Succeeded;
//        }

//        /// <summary>
//        /// Вспомогательный метод для рекурсивного поиска воздуховода по цепочке коннекторов.
//        /// </summary>
//        private Duct FindDuct(Connector currentConnector, HashSet<int> visitedElements)
//        {
//            // Пробегаемся по всем внешним ссылкам (тем коннекторам, которые состыкованы с текущим)
//            foreach (Connector refConnector in currentConnector.AllRefs)
//            {
//                Element neighbor = refConnector.Owner;
//                if (neighbor == null) continue;

//                // Если этот элемент мы уже проходили — игнорируем его, чтобы программа не зациклилась
//                if (visitedElements.Contains(neighbor.Id.IntegerValue)) continue;

//                // ЕСЛИ СОСЕД — ВОЗДУХОВОД: цель достигнута, возвращаем его
//                if (neighbor is Duct duct)
//                {
//                    return duct;
//                }

//                // ЕСЛИ СОСЕД — ДРУГОЙ ФИТИНГ: «пробиваем» его насквозь
//                if (neighbor is FamilyInstance nextFitting && nextFitting.MEPModel?.ConnectorManager != null)
//                {
//                    // Добавляем этот фитинг в список посещенных
//                    visitedElements.Add(nextFitting.Id.IntegerValue);

//                    // Проверяем все его разъемы, чтобы продолжить путь
//                    foreach (Connector nextConnector in nextFitting.MEPModel.ConnectorManager.Connectors)
//                    {
//                        // Пропускаем тот разъем, через который мы только что «вошли» в этот фитинг
//                        if (nextConnector.Id == refConnector.Id) continue;

//                        // Уходим на следующий уровень рекурсии
//                        Duct foundDuct = FindDuct(nextConnector, visitedElements);

//                        // Если на конце цепочки нашелся воздуховод — передаем его обратно наверх по стеку вызовов
//                        if (foundDuct != null) return foundDuct;
//                    }
//                }
//            }

//            return null; // Воздуховод на этой ветке не найден
//        }
//    }
//}
