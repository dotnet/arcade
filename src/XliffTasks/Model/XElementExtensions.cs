using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace XliffTasks.Model
{
    internal static class XElementExtensions
    {
        private static XNamespace XliffNS = "urn:oasis:names:tc:xliff:document:1.2";
        private static XNamespace XsiNS = "http://www.w3.org/2001/XMLSchema-instance";
        private static XName Xliff = XliffNS + "xliff";
        private static XName File = XliffNS + "file";
        private static XName Body = XliffNS + "body";
        private static XName Group = XliffNS + "group";
        private static XName TransUnit = XliffNS + "trans-unit";
        private static XName Source = XliffNS + "source";
        private static XName Target = XliffNS + "target";
        private static XName Note = XliffNS + "note";

        public static string GetTargetValueOrDefault(this XElement transUnitElement) => 
            transUnitElement.Element(Target)?.Value
            ?? transUnitElement.Element(Source).Value;

        public static void SetTargetValue(this XElement transUnitElement, string value)
        {
            XElement targetElement = transUnitElement.Element(Target);
            if (targetElement == null)
            {
                XElement sourceElement = transUnitElement.Element(Source);
                targetElement = new XElement(Target);
                sourceElement.AddAfterSelf(targetElement);
            }

            targetElement.Value = value;
        }

        public static string GetTargetStateOrDefault(this XElement transUnitElement) =>
            transUnitElement.Element(Target)?.Attribute("state")?.Value
            ?? "new";

        public static void SetTargetState(this XElement transUnitElement, string value)
        {
            XElement targetElement = transUnitElement.Element(Target);
            if (targetElement == null)
            {
                XElement sourceElement = transUnitElement.Element(Source);
                targetElement = new XElement(Target);
                sourceElement.AddAfterSelf(targetElement);
            }

            XAttribute stateAttribute = targetElement.Attribute("state");
            if (stateAttribute == null)
            {
                stateAttribute = new XAttribute("state", value);
                targetElement.Add(stateAttribute);
            }
            else
            {
                stateAttribute.Value = value;
            }
        }
    }
}
