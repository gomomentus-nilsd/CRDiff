using CrystalDecisions.CrystalReports.Engine;
//using CrystalDecisions;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace CRSerializer
{
    internal static class Extensions
    {
        public static void WriteProperty(this JsonWriter jw, string name, object value)
        {
            jw.WritePropertyName(name);
            jw.WriteValue(value);
        }

        public static void WriteArray(this JsonWriter jw, string name, object[] value)
        {

        }

        public static void WriteObjectHierarchy(this JsonWriter jw, object obj)
        {
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CustomJsonResolver(),
                Formatting = Formatting.Indented
            };
            jw.WriteRawValue(JsonConvert.SerializeObject(obj, jsonSerializerSettings));
        }

        public static void WriteObjectHierarchy(this JsonWriter jw, string name, object obj)
        {
            jw.WritePropertyName(name);
            jw.WriteObjectHierarchy(obj);
        }

        public static void WriteDataSource(this JsonWriter jw, string keyName, CrystalDecisions.ReportAppServer.DataDefModel.Tables tables)
        {
            // Collect in two passes to handle reports that mix command tables and regular tables
            var commands = new System.Collections.Generic.List<string>();
            var tableNames = new System.Collections.Generic.List<string>();

            foreach (dynamic table in tables)
            {
                if (table.ClassName == "CrystalReports.CommandTable")
                    commands.Add((string)table.CommandText);
                else
                    tableNames.Add((string)table.Name);
            }

            jw.WritePropertyName(keyName);
            jw.WriteStartObject();

            if (commands.Count > 0)
            {
                jw.WritePropertyName("Command");
                MultiLinesToArray(jw, commands[0]); // typically only one command per datasource
            }

            if (tableNames.Count > 0)
            {
                jw.WritePropertyName("Tables");
                jw.WriteStartArray();
                foreach (var name in tableNames)
                    jw.WriteValue(name);
                jw.WriteEndArray();
            }

            jw.WriteEndObject();
        }

        public static void MultiLinesToArray(this JsonWriter jw, string str)
        {
            string[] vs = str.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None);
            jw.WriteStartArray();
            foreach (var s in vs)
            {
                jw.WriteValue(s);
            }
            jw.WriteEnd();
        }

        public static void WriteParameters(this JsonWriter jw, string keyName, ReportDocument rpt)
        {
            //var parameterList = new List<string>();
            var parameterFields = rpt.DataDefinition.ParameterFields;
            jw.WritePropertyName(keyName);
            jw.WriteStartArray();
            foreach (ParameterFieldDefinition parameterField in parameterFields)
            {
                jw.WriteStartObject();
                jw.WriteProperty("Name", parameterField.Name);
                jw.WriteProperty("FormulaName", parameterField.FormulaName);
                jw.WriteProperty("ValueType", parameterField.ValueType.ToString());
                jw.WriteProperty("EnableNullValue", parameterField.EnableNullValue.ToString());
                jw.WriteEndObject();
            }
            jw.WriteEnd();
        }
        private static List<string> Parameters(ReportDocument rpt)
        {
            var paramtrs = new List<string>();
            var parameters = rpt.DataDefinition.ParameterFields;
            foreach (ParameterFieldDefinition param in parameters)
            {
                paramtrs.Add($"Parameter {param.ReportName}.{param.Name}");
            }
            return paramtrs;
        }

        public static void WritePrintOptions(this JsonWriter jw, PrintOptions printOptions)
        {
            jw.WritePropertyName("PrintOptions");
            jw.WriteStartObject();
            jw.WriteProperty("NoPrinter", printOptions.NoPrinter);
            jw.WriteProperty("PrinterName", printOptions.PrinterName);
            jw.WriteProperty("SavedPrinterName", printOptions.SavedPrinterName);
            jw.WriteProperty("PrinterDuplex", printOptions.PrinterDuplex.ToString());
            jw.WriteProperty("PaperOrientation", printOptions.PaperOrientation.ToString());
            jw.WriteProperty("PaperSize", printOptions.PaperSize.ToString());
            jw.WriteProperty("PaperSource", printOptions.PaperSource.ToString());
            jw.WriteProperty("DissociatePageSizeAndPrinterPaperSize", printOptions.DissociatePageSizeAndPrinterPaperSize);
            jw.WriteProperty("PageContentHeight", $"{printOptions.PageContentHeight} twips ({printOptions.PageContentHeight / 1440.0:N3} inches)");
            jw.WriteProperty("PageContentWidth", $"{printOptions.PageContentWidth} twips ({printOptions.PageContentWidth / 1440.0:N3} inches)");

            jw.WritePropertyName("PageMargins");
            jw.WriteStartObject();
            jw.WriteProperty("topMargin", $"{printOptions.PageMargins.topMargin} twips ({printOptions.PageMargins.topMargin / 1440.0:N3} inches)");
            jw.WriteProperty("leftMargin", $"{printOptions.PageMargins.leftMargin} twips ({printOptions.PageMargins.leftMargin / 1440.0:N3} inches)");
            jw.WriteProperty("rightMargin", $"{printOptions.PageMargins.rightMargin} twips ({printOptions.PageMargins.rightMargin / 1440.0:N3} inches)");
            jw.WriteProperty("bottomMargin", $"{printOptions.PageMargins.bottomMargin} twips ({printOptions.PageMargins.bottomMargin / 1440.0:N3} inches)");

            jw.WriteEndObject(); // closes PageMargins
            jw.WriteEndObject(); // closes PrintOptions
        }

        /// <summary>
        /// Writes the conditional formulas (Suppress, Can Grow, Keep Together, ... ) of every area, section and
        /// report object. The Engine objects dumped by WriteObjectHierarchy do not expose them, so they are read
        /// from the RAS report definition. See docs/adr/0001-serialize-condition-formulas-via-ras.md.
        /// </summary>
        public static void WriteConditionFormulas(this JsonWriter jw, string keyName, dynamic reportDefinition)
        {
            jw.WritePropertyName(keyName);
            jw.WriteStartArray();
            foreach (dynamic area in Items(reportDefinition.Areas))
            {
                string areaName = area.Name;
                WriteSectionAreaConditionFormulas(jw, "Area", areaName, areaName, area.Format.ConditionFormulas);
                foreach (dynamic section in Items(area.Sections))
                {
                    string sectionName = section.Name;
                    string sectionPath = areaName + "/" + sectionName;
                    WriteSectionAreaConditionFormulas(jw, "Section", sectionName, sectionPath, section.Format.ConditionFormulas);
                    foreach (dynamic reportObject in Items(section.ReportObjects))
                    {
                        string objectName = reportObject.Name;
                        WriteObjectConditionFormulas(jw, "Object", objectName, sectionPath + "/" + objectName, reportObject.Format.ConditionFormulas);
                    }
                }
            }
            jw.WriteEndArray();
        }

        // RAS collections are COM wrappers without IEnumerable support, so iterate by index.
        private static IEnumerable<dynamic> Items(dynamic collection)
        {
            int count = collection.Count;
            for (int i = 0; i < count; i++)
                yield return collection[i];
        }

        // The RAS format objects come back as bare COM objects, so they are cast to the typed interfaces to reach
        // the Formula[enum] getter. Enumerating the enum values keeps the condition names out of this code.
        private static void WriteSectionAreaConditionFormulas(JsonWriter jw, string kind, string name, string path, object conditionFormulas)
        {
            var formulas = (CrystalDecisions.ReportAppServer.ReportDefModel.ISCRSectionAreaFormatConditionFormulas)conditionFormulas;
            foreach (CrystalDecisions.ReportAppServer.ReportDefModel.CrSectionAreaFormatConditionFormulaTypeEnum condition
                in System.Enum.GetValues(typeof(CrystalDecisions.ReportAppServer.ReportDefModel.CrSectionAreaFormatConditionFormulaTypeEnum)))
            {
                var formula = formulas[condition];
                WriteConditionFormulaEntry(jw, kind, name, path, condition.ToString(), formula == null ? null : formula.Text);
            }
        }

        private static void WriteObjectConditionFormulas(JsonWriter jw, string kind, string name, string path, object conditionFormulas)
        {
            var formulas = (CrystalDecisions.ReportAppServer.ReportDefModel.ISCRObjectFormatConditionFormulas)conditionFormulas;
            foreach (CrystalDecisions.ReportAppServer.ReportDefModel.CrObjectFormatConditionFormulaTypeEnum condition
                in System.Enum.GetValues(typeof(CrystalDecisions.ReportAppServer.ReportDefModel.CrObjectFormatConditionFormulaTypeEnum)))
            {
                var formula = formulas[condition];
                WriteConditionFormulaEntry(jw, kind, name, path, condition.ToString(), formula == null ? null : formula.Text);
            }
        }

        private static void WriteConditionFormulaEntry(JsonWriter jw, string kind, string name, string path, string condition, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            jw.WriteStartObject();
            jw.WriteProperty("Kind", kind);
            jw.WriteProperty("Name", name);
            jw.WriteProperty("Path", path);
            jw.WriteProperty("Condition", condition);
            jw.WriteProperty("Text", text);
            jw.WriteEndObject();
        }
    }
}