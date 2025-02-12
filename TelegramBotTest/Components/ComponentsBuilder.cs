﻿using ElizerBot.Adapter;
using ElizerBot.Adapter.Triggers;
using TelegramBotTest.Utils;

namespace TelegramBotTest.Components
{
    public class ComponentsBuilder
    {
        public static IReadOnlyCollection<CommandTrigger<Context>> BuildCommands()
        {
            return new[]
            {
                new CommandTrigger<Context>("setthischat", async (c, a) =>
                {
                    if (a.Chat.IsPrivate)
                        await a.Bot.SendRandomEmo(a.Chat);
                    else
                        await c.SetupTargetChat(a.Chat.Id);
                }),
                new CommandTrigger<Context>("newrequest", async (c, a) =>
                {
                    if (a.Chat.IsPrivate)
                    {
                        var newTicket = new Ticket();
                        c.EditingTickets[a.User.Id] = newTicket;
                        var message = new NewMessageAdapter(a.Chat)
                        {
                            Text = $"{c.Template.FieldNames[0]}:",
                        };
                        await a.Bot.SendMessage(message);
                    }
                    else
                        await a.Bot.SendRandomEmo(a.Chat);
                }),
                new CommandTrigger<Context>("subscribereport", async (c, a) =>
                {
                    if (a.Chat.IsPrivate)
                        await c.SubscribeReport(a.Chat.Id);
                    else
                        await a.Bot.SendRandomEmo(a.Chat);
                }),
                new CommandTrigger<Context>("unsubscribereport", async (c, a) =>
                {
                    if (a.Chat.IsPrivate)
                        await c.UnsubscibeReport(a.Chat.Id);
                    else
                        await a.Bot.SendRandomEmo(a.Chat);
                })
            };
        }

        public static IReadOnlyCollection<MessageTrigger<Context>> BuildMessages()
        {
            return new[]
            {
                new PrivateChatMessageTrigger(async (c, a) =>
                {
                    if (c.EditingTickets.TryGetValue(a.Message.User.Id, out var ticket))
                    {
                        ticket.Fields[c.Template.FieldNames[ticket.EditState]] = a.Message.Text;
                        ticket.EditState++;
                        if (ticket.EditState < c.Template.FieldNames.Length)
                        {
                            var message = new NewMessageAdapter(a.Message.Chat)
                            {
                                Text = $"{c.Template.FieldNames[ticket.EditState]}:",
                            };
                            await a.Bot.SendMessage(message);
                        }
                        else
                        {
                            c.EditingTickets.Remove(a.Message.User.Id);
                            var completedMessage = new NewMessageAdapter(a.Message.Chat)
                            {
                                Text = c.Template.CompletedMessage.InsertEmo(),
                            };
                            await a.Bot.SendMessage(completedMessage);

                            var targetChatId = string.IsNullOrEmpty(c.Settings.TargetChatId) || string.Equals(c.Settings.TargetChatId, "0")
                                ? a.Message.Chat.Id
                                : c.Settings.TargetChatId;
                            var targetChat = new ChatAdapter(targetChatId, false);

                            var ticketTextLines = new List<string>
                            {
                                c.Template.NewTicketTitle.InsertEmo()
                            };
                            ticketTextLines.AddRange(ticket.Fields.Select(p => $"{p.Key}: {p.Value}"));
                            ticketTextLines.Add(GetHistoryRecord(c.Template.NewTicketHistory, a.Message.User));
                            var ticketMessage = new NewMessageAdapter(targetChat)
                            {
                                Text = ticketTextLines.JoinLines(),
                                Buttons = new []
                                {
                                    new[]
                                    {
                                        new ButtonAdapter
                                        {
                                            Data = "accept",
                                            Label = c.Template.AcceptButtonLabel.InsertEmo()
                                        }
                                    }
                                }
                            };
                            var ticketFeedback = await a.Bot.SendMessage(ticketMessage);
                            await c.Repository.Save(ticketFeedback.Id, ticketFeedback.Text);
                        }
                    }
                })
            };
        }

        private static string GetHistoryRecord(string template, UserAdapter user)
        {
            var userFullName = $"{user.FirstName} {user.LastName}".Trim();
            var userMoniker = string.IsNullOrEmpty(userFullName)
                ? user.Username
                : $"{user.Username} ({userFullName})";
            return template.Replace("<user>", userMoniker).Replace("<time>", DateTime.Now.ToShortTimeString());
        }

        public static IReadOnlyCollection<Trigger<Context, ButtonTriggerArgument>> BuildButtons()
        {
            return new Trigger<Context, ButtonTriggerArgument>[]
            {
                new ButtonTrigger<Context>("accept", async (c, a) =>
                {
                    var postedMessage = a.Message;
                    var text = postedMessage.Text.SplitLines().ToList();
                    text.RemoveAt(0);
                    text.Insert(0, c.Template.AcceptedTicketTitle.InsertEmo());
                    text.Add(GetHistoryRecord(c.Template.AcceptedTicketHistory, a.User).InsertEmo());
                    postedMessage.Text = text.JoinLines();
                    postedMessage.Buttons = new[]
                    {
                        new []
                        {
                            new ButtonAdapter
                            {
                                Data = $"done${a.User.Id}",
                                Label = c.Template.DoneButtonLabel.InsertEmo()
                            },
                            new ButtonAdapter
                            {
                                Data = $"drop${a.User.Id}",
                                Label = c.Template.DropButtonLabel.InsertEmo()
                            }
                        }
                    };
                    var feedback = await a.Bot.EditMessage(postedMessage);
                    await c.Repository.Save(postedMessage.Id, postedMessage.Text);
                }),
                new ParametrizedButtonTrigger<Context>("done", "$", async (c, a) =>
                {
                    var userId = a.Data.Split("$").Skip(1).FirstOrDefault();
                    if (string.Equals(userId, a.User.Id))
                    {
                        var postedMessage = a.Message;
                        var text = postedMessage.Text.SplitLines().ToList();
                        text.RemoveAt(0);
                        text.Insert(0, c.Template.DoneTicketTitle.InsertEmo());
                        text.Add(GetHistoryRecord(c.Template.DoneTicketHistory, a.User).InsertEmo());
                        postedMessage.Text = text.JoinLines();
                        postedMessage.Buttons = null;
                        var feedback = await a.Bot.EditMessage(postedMessage);
                        await c.Repository.Save(postedMessage.Id, postedMessage.Text);
                    }
                }),
                new ParametrizedButtonTrigger<Context>("drop", "$", async (c, a) =>
                {
                    var userId = a.Data.Split("$").Skip(1).FirstOrDefault();
                    if (string.Equals(userId, a.User.Id))
                    {
                        var postedMessage = a.Message;
                        var text = postedMessage.Text.SplitLines().ToList();
                        text.RemoveAt(0);
                        text.Insert(0, c.Template.DropTicketTitle.InsertEmo());
                        text.Add(GetHistoryRecord(c.Template.DropTicketHistory, a.User).InsertEmo());
                        postedMessage.Text = text.JoinLines();
                        postedMessage.Buttons = null;
                        var feedback = await a.Bot.EditMessage(postedMessage);
                        await c.Repository.Save(postedMessage.Id, postedMessage.Text);
                    }
                })
            };
        }
    }
}
