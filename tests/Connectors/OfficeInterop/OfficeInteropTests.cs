using System.Collections.Generic;

namespace InfrastructureTools.Connectors.OfficeInterop.Tests;

public class OfficeInteropTests
{
    [Fact]
    public void VerifyEmail()
    {
        OutlookCommunicator c = new();
        ItemOptions itemOptions = new()
        {
            Subject = "MySubject",
            Body = "MyBody",
            Importance = ItemImportance.High,
            Recipients =
          [
              new RecipientOptions
          {
              Email = "mail1@email.com",
              Required = true
          }
          ]
        };
        c.CreateEmail(itemOptions);
    }

    [Fact]
    public void VerifyMeeting()
    {
        OutlookCommunicator c = new();
        List<RecipientOptions> recipients = [
            new RecipientOptions
            {
                Email = "mail1@email.com",
                Required = true
            },
            new RecipientOptions
            {
                Email = "mail2@email.com",
                Required = false
            }
        ];
        ItemOptions itemOptions = new()
        {
            Subject = "MySubject",
            Body = "MyBody",
            Importance = ItemImportance.Low,
            Recipients = recipients
        };
        c.CreateMeeting(itemOptions);
    }

    [Fact]
    public void VerifyAppointment()
    {
        OutlookCommunicator c = new();
        List<RecipientOptions> recipients = [
            new RecipientOptions
            {
                Email = "mail1@email.com",
                Required = true
            },
            new RecipientOptions
            {
                Email = "mail2@email.com",
                Required = false
            }
        ];
        ItemOptions itemOptions = new()
        {
            Subject = "MySubject",
            Body = "MyBody",
            Importance = ItemImportance.Low,
            Recipients = recipients
        };
        c.CreateAppointment(itemOptions);
    }
}