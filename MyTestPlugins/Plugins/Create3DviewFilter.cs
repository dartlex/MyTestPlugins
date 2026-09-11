using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyTestPlugins.Plugins
{
    [Transaction(TransactionMode.Manual)]
    public class Create3DviewFilter : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc.Document;
            var allElements = new FilteredElementCollector(doc).WhereElementIsNotElementType().ToElements();
            HashSet<string> systemNames = new HashSet<string>();
            foreach (var element in allElements)
            {
                Parameter param = element.get_Parameter(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);
                if (param != null && param.HasValue && !string.IsNullOrEmpty(param.AsString()))
                {
                    string currentSystemName = param.AsString();
                    systemNames.Add(currentSystemName);
                }
            }
            List<string> sortedSystems = systemNames.OrderBy(s => s).ToList();

            UserControl1 window = new UserControl1(sortedSystems);

            if (window.ShowDialog() == true)
            {
                string selectedSystem = window.SelectedSystemName;

                // 1. Находим тип 3D вида
                ViewFamilyType viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                // 2. Получаем ID параметра "Имя системы"
                ElementId syncParamId = new ElementId(BuiltInParameter.RBS_SYSTEM_NAME_PARAM);

                // 3. Собираем список MEP категорий
                List<ElementId> mepCategories = new List<ElementId>
                {
                    new ElementId(BuiltInCategory.OST_DuctAccessory),      // Арматура воздуховодов
                    new ElementId(BuiltInCategory.OST_PipeAccessory),      // Арматура трубопроводов
                    new ElementId(BuiltInCategory.OST_DuctCurves),         // Воздуховоды
                    new ElementId(BuiltInCategory.OST_DuctTerminal),       // Воздухораспределители
                    new ElementId(BuiltInCategory.OST_FlexDuctCurves),     // Гибкие воздуховоды
                    new ElementId(BuiltInCategory.OST_FlexPipeCurves),     // Гибкие трубы
                    new ElementId(BuiltInCategory.OST_DuctLinings),        // Материалы внутренней изоляции воздуховодов
                    new ElementId(BuiltInCategory.OST_DuctInsulations),    // Материалы изоляции воздуховодов
                    new ElementId(BuiltInCategory.OST_PipeInsulations),    // Материалы изоляции труб
                    new ElementId(BuiltInCategory.OST_MechanicalEquipment), // Оборудование (Механическое)
                    new ElementId(BuiltInCategory.OST_PlumbingFixtures),   // Сантехнические приборы
                    new ElementId(BuiltInCategory.OST_DuctFitting),        // Соединительные детали воздуховодов
                    new ElementId(BuiltInCategory.OST_PipeFitting),        // Соединительные детали трубопроводов
                    new ElementId(BuiltInCategory.OST_PipeCurves)          // Трубы
                };

                // 4. Работаем внутри транзакции
                using (Transaction tx = new Transaction(doc, "Создать аксонометрию"))
                {
                    tx.Start();

                    // Создаем 3D вид
                    View3D newView = View3D.CreateIsometric(doc, viewFamilyType.Id);

                    // Логика автоматического подбора имени с индексами (1), (2) и т.д.
                    string targetViewName = selectedSystem;
                    int counter = 1;

                    HashSet<string> existingViewNames = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Select(v => v.Name)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    while (existingViewNames.Contains(targetViewName))
                    {
                        targetViewName = $"{selectedSystem}({counter})";
                        counter++;
                    }

                    newView.Name = targetViewName;

                    // ФИЛЬТР 1: Скрываем чужие стандартные MEP-системы
                    FilterRule ruleNotEqual = ParameterFilterRuleFactory.CreateNotEqualsRule(syncParamId, selectedSystem, false);
                    ElementParameterFilter filterNotEqual = new ElementParameterFilter(ruleNotEqual);

                    ParameterFilterElement viewFilter = new FilteredElementCollector(doc)
                        .OfClass(typeof(ParameterFilterElement))
                        .Cast<ParameterFilterElement>()
                        .FirstOrDefault(f => f.Name.Equals(selectedSystem, StringComparison.OrdinalIgnoreCase));

                    if (viewFilter == null)
                    {
                        viewFilter = ParameterFilterElement.Create(doc, selectedSystem, mepCategories);
                    }
                    viewFilter.SetCategories(mepCategories);
                    viewFilter.SetElementFilter(filterNotEqual);

                    if (!newView.GetFilters().Contains(viewFilter.Id)) newView.AddFilter(viewFilter.Id);
                    newView.SetFilterVisibility(viewFilter.Id, false);


                    // ФИЛЬТР 2 ДЛЯ КУБИКОВ (Обобщенных моделей): Скрываем их по параметру "ИмяСистемы"
                    Element genericSample = new FilteredElementCollector(doc)
                        .OfCategory(BuiltInCategory.OST_GenericModel)
                        .WhereElementIsNotElementType()
                        .FirstOrDefault();

                    if (genericSample != null)
                    {
                        Parameter customParam = genericSample.LookupParameter("ИмяСистемы");
                        if (customParam != null)
                        {
                            ElementId customParamId = customParam.Id;

                            FilterRule customNotEqual = ParameterFilterRuleFactory.CreateNotEqualsRule(customParamId, selectedSystem, false);
                            ElementParameterFilter filterCustomNotEqual = new ElementParameterFilter(customNotEqual);

                            FilterRule customEmpty = ParameterFilterRuleFactory.CreateEqualsRule(customParamId, "", false);
                            ElementParameterFilter filterCustomEmpty = new ElementParameterFilter(customEmpty);

                            LogicalOrFilter combinedCustomFilter = new LogicalOrFilter(new List<ElementFilter> { filterCustomNotEqual, filterCustomEmpty });

                            List<ElementId> genericCatList = new List<ElementId> { new ElementId(BuiltInCategory.OST_GenericModel) };
                            string customFilterName = "Скрыть_Кубики_" + selectedSystem;

                            ParameterFilterElement customViewFilter = new FilteredElementCollector(doc)
                                .OfClass(typeof(ParameterFilterElement))
                                .Cast<ParameterFilterElement>()
                                .FirstOrDefault(f => f.Name.Equals(customFilterName, StringComparison.OrdinalIgnoreCase));

                            if (customViewFilter == null)
                            {
                                customViewFilter = ParameterFilterElement.Create(doc, customFilterName, genericCatList);
                            }
                            customViewFilter.SetCategories(genericCatList);
                            customViewFilter.SetElementFilter(combinedCustomFilter);

                            if (!newView.GetFilters().Contains(customViewFilter.Id)) newView.AddFilter(customViewFilter.Id);
                            newView.SetFilterVisibility(customViewFilter.Id, false);
                        }
                    }

                    // Задаем жесткую ориентацию 3D вида Сверху-Спереди-Справа (аксонометрия)
                    XYZ forward = new XYZ(-1, -1, -1).Normalize();
                    XYZ up = new XYZ(-1, -1, 2).Normalize();
                    ViewOrientation3D orientation = new ViewOrientation3D(XYZ.Zero, up, forward);
                    newView.SetOrientation(orientation);

                    // Намертво скрываем оси на этом виде
                    ElementId gridCatId = new ElementId(BuiltInCategory.OST_Grids);
                    if (newView.CanCategoryBeHidden(gridCatId)) newView.SetCategoryHidden(gridCatId, true);

                    // Намертво скрываем уровни на этом виде
                    ElementId levelCatId = new ElementId(BuiltInCategory.OST_Levels);
                    if (newView.CanCategoryBeHidden(levelCatId)) newView.SetCategoryHidden(levelCatId, true);

                    // Намертво скрываем контейнеры групп моделей на этом виде
                    ElementId groupCatId = new ElementId(BuiltInCategory.OST_IOSModelGroups);
                    if (newView.CanCategoryBeHidden(groupCatId)) newView.SetCategoryHidden(groupCatId, true);

                    // Полностью скрываем связанные файлы Revit на этом виде
                    ElementId linkCategoryId = new ElementId(BuiltInCategory.OST_RvtLinks);
                    if (newView.CanCategoryBeHidden(linkCategoryId)) newView.SetCategoryHidden(linkCategoryId, true);

                    tx.Commit();

                    // Открываем созданный вид на экране
                    uiDoc.ActiveView = newView;
                }
            }

            return Result.Succeeded;
        }
    }
}
