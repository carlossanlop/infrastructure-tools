using System.Collections.Generic;
using InfrastructureTools.Connectors.OfficeInterop;

public class OfficeInteropTests
{
    [Fact]
    public void VerifyEmail()
    {
        OutlookCommunicator c = new();
        c.CreateEmail(new ItemOptions
        {
            Subject = "MySubject",
            Body = "MyBody",
            Importance = ItemImportance.High,
            Recipients = new List<RecipientOptions>
            {
                new RecipientOptions
                {
                    Email = "mail1@email.com",
                    Required = true
                }
            }
        });
    }

    [Fact]
    public void VerifyMeeting()
    {

    }
}