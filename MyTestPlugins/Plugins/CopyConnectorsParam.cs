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

