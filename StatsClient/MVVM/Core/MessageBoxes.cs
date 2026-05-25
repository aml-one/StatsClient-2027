using Ookii.Dialogs.Wpf;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using TaskDialog = Ookii.Dialogs.Wpf.TaskDialog;
using TaskDialogButton = Ookii.Dialogs.Wpf.TaskDialogButton;
using TaskDialogIcon = Ookii.Dialogs.Wpf.TaskDialogIcon;

namespace StatsClient.MVVM.Core
{
    public static class MessageBoxes
    {
        public static void ShowMessage(Window Owner, string MessageBoxTitle, string MessageTitle, string MessageBody, 
                                    string ExpandedInformation, string Footer, TaskDialogIcon MainIcon, TaskDialogIcon FooterIcon, Buttons MessageButtons)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = MessageBoxTitle;
                    dialog.MainInstruction = MessageTitle;
                    dialog.MainIcon = MainIcon;
                    dialog.Content = MessageBody;
                    dialog.ExpandedInformation = ExpandedInformation;
                    dialog.Footer = Footer;
                    dialog.FooterIcon = FooterIcon;
                    dialog.CenterParent = true;

                    switch(MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody, MessageBoxTitle);
            }
            
        }

        public static void ShowMessage(Window Owner, string MessageBoxTitle, string MessageTitle, string MessageBody,
                                    string ExpandedInformation, string Footer, TaskDialogIcon MainIcon, Buttons MessageButtons)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = MessageBoxTitle;
                    dialog.MainInstruction = MessageTitle;
                    dialog.MainIcon = MainIcon;
                    dialog.Content = MessageBody;
                    dialog.ExpandedInformation = ExpandedInformation;
                    dialog.Footer = Footer;                    
                    dialog.CenterParent = true;

                    switch (MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody, MessageBoxTitle);
            }

        }




        public static void ShowMessage(Window Owner, string MessageBoxTitle, string MessageTitle, string MessageBody,
                                    string ExpandedInformation, TaskDialogIcon Icon, Buttons MessageButtons)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = MessageBoxTitle;
                    dialog.MainInstruction = MessageTitle;
                    dialog.Content = MessageBody;
                    dialog.ExpandedInformation = ExpandedInformation;
                    dialog.FooterIcon = Icon;
                    dialog.CenterParent = true;

                    switch (MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody, MessageBoxTitle);
            }

        }






        public static void ShowMessage(Window Owner, string MessageBoxTitle, string MessageTitle, string MessageBody,
                                    TaskDialogIcon MainIcon, Buttons MessageButtons)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = MessageBoxTitle;
                    dialog.MainInstruction = MessageTitle;
                    dialog.Content = MessageBody;                    
                    dialog.MainIcon = MainIcon;
                    dialog.CenterParent = true;

                    switch (MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody, MessageBoxTitle);
            }

        }




        public static void ShowMessage(Window Owner, string MessageBoxTitle, string MessageTitle, string MessageBody,
                                      TaskDialogIcon MainIcon, Buttons MessageButtons, string Footer)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = MessageBoxTitle;
                    dialog.MainInstruction = MessageTitle;
                    dialog.MainIcon = MainIcon;
                    dialog.Content = MessageBody;                    
                    dialog.Footer = Footer;
                    dialog.CenterParent = true;

                    switch (MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody, MessageBoxTitle);
            }

        }



        public static void ShowMessage(Window Owner, string MessageBody, TaskDialogIcon MainIcon, Buttons MessageButtons)
        {
            if (TaskDialog.OSSupportsTaskDialogs)
            {
                using (TaskDialog dialog = new TaskDialog())
                {
                    dialog.WindowTitle = "";
                    dialog.Content = MessageBody;                    
                    dialog.CenterParent = true;
                    dialog.MainIcon = MainIcon;

                    switch (MessageButtons)
                    {
                        case Buttons.Ok:
                            TaskDialogButton okButton = new TaskDialogButton(ButtonType.Ok);
                            dialog.Buttons.Add(okButton);
                            break;

                        case Buttons.Close:
                            TaskDialogButton closeButton = new TaskDialogButton(ButtonType.Close);
                            dialog.Buttons.Add(closeButton);
                            break;
                    }

                    TaskDialogButton buttonResult = dialog.ShowDialog(Owner);
                }
            }
            else
            {
                MessageBox.Show(Owner, MessageBody);
            }

        }


        public enum Buttons
        {
            Ok = 0,
            
            OkCancel = 1,
            
            YesNo = 2,
            
            YesNoCancel = 3,
            
            RetryCancel = 4,
            
            RetryIgnoreCancel = 5,
            
            Close = 6
        }
    }
}
