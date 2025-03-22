using Octokit;
using System.Collections.Generic;
using System.Xml.Linq;
namespace InfrastructureTools.CommitCollector;

public class PullRequestPeople
{
    public string Author { get; set; }
    public SortedList<string, string> Approvers { get; } // alias -> name
    
    public PullRequestPeople(string commitCreator)
    {
        Author = commitCreator;
        Approvers = new SortedList<string, string>();
    }

    //public void Update(User user)
    //{
    //    string name = GetNameOrUserName(user);
    //    if (Author == string.Empty || (Author == CommitCollector.UserNameGitHubActionsBot && name != CommitCollector.UserNameGitHubActionsBot))
    //    {
    //        Author = name;
    //    }
    //    if (Author != name && !Approvers.ContainsKey(user.Login))
    //    {
    //        // Only add this user to approvers if it is not the author
    //        Approvers.Add(user.Login, name);
    //    }
    //}

    public bool TryAddAuthor(User user)
    {
        string name = GetNameOrUserName(user);
        if (name != CommitCollector.UserNameGitHubActionsBot)
        {
            Approvers.Remove(user.Login);
            Author = name;
            return true;
        }
        return false;
    }

    public bool TryAddApprover(User user)
    {
        string name = GetNameOrUserName(user);
        if (name != CommitCollector.UserNameGitHubActionsBot && Author != name && !Approvers.ContainsKey(user.Login))
        {
            Approvers.Add(user.Login, name);
            return true;
        }
        return false;
    }

    private string GetNameOrUserName(User user) => string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name;
}