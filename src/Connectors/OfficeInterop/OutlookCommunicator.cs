using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace InfrastructureTools.Connectors.OfficeInterop;

public class OutlookCommunicator
{
    public OutlookCommunicator()
    {
        if (Process.GetProcessesByName("OUTLOOK").Length > 0)
        {
            Application = GetActiveObject("Outlook.Application") as Outlook.Application ??
                throw new NullReferenceException("Could not retrieve the existing Outlook Application instance.");
        }
        else
        {
            Application = new Outlook.Application();
            Outlook.NameSpace? ns = Application.GetNamespace("MAPI");
            ns.Logon("", "", Missing.Value, Missing.Value);
        }
    }

    public Outlook.Application Application { get; }

    public void CreateMeeting(MeetingOptions options)
    {
        Outlook.AppointmentItem appt =
            (Outlook.AppointmentItem)Application.CreateItem(Outlook.OlItemType.olAppointmentItem) ??
            throw new NullReferenceException("Could not create an appointment item.");

        appt.MeetingStatus = Outlook.OlMeetingStatus.olNonMeeting;
        appt.Location = options.Location;
        appt.Subject = options.Subject;
        appt.Body = options.Body;
        appt.Start = options.Start;
        appt.Duration = options.Duration;
        appt.Importance = options.Importance;
        appt.BusyStatus = options.BusyStatus;

        foreach (string recipientEmail in options.Recipients)
        {
            Outlook.Recipient recipient = appt.Recipients.Add(recipientEmail);
            recipient.Type = (int)Outlook.OlMeetingRecipientType.olRequired;
        }

        appt.Display();
    }

    private static object GetActiveObject(string progId)
    {
        ArgumentNullException.ThrowIfNull(progId);

        int hr = CLSIDFromProgIDEx(progId, out Guid clsid);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = GetActiveObject(clsid, IntPtr.Zero, out object obj);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        return obj;
    }

    [DllImport("ole32")]
    private static extern int CLSIDFromProgIDEx([MarshalAs(UnmanagedType.LPWStr)] string lpszProgID, out Guid lpclsid);

    [DllImport("oleaut32")]
    private static extern int GetActiveObject([MarshalAs(UnmanagedType.LPStruct)] Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);
}