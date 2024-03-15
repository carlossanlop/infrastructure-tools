using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Office.Interop.Outlook;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace InfrastructureTools.Connectors.OfficeInterop;

public class OutlookCommunicator
{
    public OutlookCommunicator()
    {
        if (Process.GetProcessesByName("OUTLOOK").Length > 0)
        {
            Application = GetActiveObject("Outlook.Application") as Application ??
                throw new NullReferenceException("Could not retrieve the existing Outlook Application instance.");
        }
        else
        {
            Application = new Application();
            NameSpace? ns = Application.GetNamespace("MAPI");
            ns.Logon("", "", Missing.Value, Missing.Value);
        }
    }

    public Outlook.Application Application { get; }

    public void CreateEmail(ItemOptions options)
    {
        MailItem mail =
            (MailItem)Application.CreateItem(OlItemType.olMailItem) ??
            throw new NullReferenceException("Could not create a mail item.");

        mail.Subject = options.Subject;
        mail.Body = options.Body;
        mail.Importance = (OlImportance)options.Importance;

        foreach (RecipientOptions r in options.Recipients)
        {
            Recipient recipient = mail.Recipients.Add(r.Email);
            recipient.Type = (int)(r.Required ? OlMailRecipientType.olTo : OlMailRecipientType.olCC);
        }

        mail.Display();
    }

    public void CreateMeeting(ItemOptions options)
    {
        AppointmentItem appt =
            (AppointmentItem)Application.CreateItem(OlItemType.olAppointmentItem) ??
            throw new NullReferenceException("Could not create an appointment item.");

        appt.MeetingStatus = OlMeetingStatus.olMeeting;
        appt.Location = options.Location;
        appt.Subject = options.Subject;
        appt.Body = options.Body;
        appt.Start = options.Start;
        appt.Duration = options.Duration;
        appt.Importance = (OlImportance)options.Importance;
        appt.BusyStatus = (OlBusyStatus)options.BusyStatus;

        foreach (RecipientOptions r in options.Recipients)
        {
            Recipient recipient = appt.Recipients.Add(r.Email);
            recipient.Type = (int)(r.Required ? OlMeetingRecipientType.olRequired : OlMeetingRecipientType.olOptional);
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