using Microsoft.Office.Interop.Outlook;

namespace InfrastructureTools.Connectors.OfficeInterop;

public enum ItemImportance
{
    Low = OlImportance.olImportanceLow,
    Normal = OlImportance.olImportanceNormal,
    High = OlImportance.olImportanceHigh
}