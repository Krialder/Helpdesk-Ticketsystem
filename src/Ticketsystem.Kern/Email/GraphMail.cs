using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Users.Item.SendMail;

namespace Ticketsystem.Kern.Email;

// Versand über Microsoft Graph mit Client-Credentials; nur Senden, kein
// Postfachabruf, deshalb genügt das Recht Mail.Send. Live gegen ein echtes
// Postfach ist dieser Weg noch nicht geprüft; siehe die bekannten
// Einschränkungen in README und CHANGELOG.
public class GraphTicketMailer(GraphServiceClient graph, IOptions<EmailOptions> options) : ITicketMailer
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken) =>
        graph.Users[options.Value.MailboxAddress].SendMail.PostAsync(new SendMailPostRequestBody
        {
            Message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Text, Content = body },
                ToRecipients = [new Recipient { EmailAddress = new EmailAddress { Address = to } }]
            },
            SaveToSentItems = true
        }, cancellationToken: cancellationToken);
}
