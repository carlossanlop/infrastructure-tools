using Outlook = Microsoft.Office.Interop.Outlook;
using System;
using System.Collections.Generic;

namespace InfrastructureTools.Connectors.OfficeInterop;

public class ItemOptions
{
    // Shared properties
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public List<RecipientOptions> Recipients { get; set; } = new();

    // Meeting properties
    public string Location { get; set; } = string.Empty;
    public DateTime Start { get; set; } = DateTime.Now;
    public int Duration { get; set; } = 30;
    public ItemImportance Importance { get; set; } = ItemImportance.Normal;
    public MeetingBusyStatus BusyStatus { get; set; } = MeetingBusyStatus.Busy;
}

