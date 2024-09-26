using Microsoft.Office.Interop.Outlook;

namespace InfrastructureTools.Connectors.OfficeInterop;

public enum MeetingBusyStatus
{
    Free = OlBusyStatus.olFree,
    Tentative = OlBusyStatus.olTentative,
    Busy = OlBusyStatus.olBusy,
    OutOfOffice = OlBusyStatus.olOutOfOffice,
    WorkingElsewhere = OlBusyStatus.olWorkingElsewhere
}
