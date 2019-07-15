using Sitecore.Collections;
using Sitecore.Data;
using Sitecore.Data.Events;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.ExperienceEditor.Utils;
using Sitecore.Globalization;
using Sitecore.Pipelines.GetPageEditorNotifications;
using Sitecore.Pipelines.HasPresentation;
using Sitecore.Resources;
using Sitecore.SecurityModel;
using Sitecore.Shell.Framework.CommandBuilders;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Sites;
using Sitecore.Web;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.WebControls.Ribbons;
using System.Collections.Specialized;
using System.Web.UI;
using System.Web.UI.HtmlControls;

namespace Sitecore.Support.Shell.Applications.WebEdit
{
    public class WebEditRibbonForm : Sitecore.Shell.Applications.WebEdit.WebEditRibbonForm
    {
        protected HtmlForm RibbonForm;

        protected Border RibbonPane;

        protected Border Treecrumb;

        protected Border Notifications;

        protected Border TreecrumbPane;

        private bool currentItemDeleted;

        private bool refreshHasBeenAsked;

        public string ContextUri
        {
            get
            {
                return (base.ServerProperties["ContextUri"] ?? this.CurrentItemUri) as string;
            }
            set
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                base.ServerProperties["ContextUri"] = value;
            }
        }

        public string CurrentItemUri
        {
            get
            {
                return base.ServerProperties["CurrentItemUri"] as string;
            }
            set
            {
                Assert.ArgumentNotNullOrEmpty(value, "value");
                base.ServerProperties["CurrentItemUri"] = value;
            }
        }

        public override void HandleMessage(Message message)
        {
           base.HandleMessage(message);
        }

        protected void ConfirmAndReload(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            if (this.currentItemDeleted)
            {
                return;
            }
            if (args.IsPostBack)
            {
                if (args.HasResult && args.Result != "no")
                {
                    SheerResponse.Eval("window.parent.location.reload(true)");
                    return;
                }
            }
            else if (!this.refreshHasBeenAsked)
            {
                SheerResponse.Confirm("An item was deleted. Do you want to refresh the page?");
                this.refreshHasBeenAsked = true;
                args.WaitForPostBack();
            }
        }

        protected virtual void CopiedNotification(object sender, ItemCopiedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Context.ClientPage.Start(this, "Reload");
        }

        protected virtual void CreatedNotification(object sender, ItemCreatedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Context.ClientPage.Start(this, "Reload");
        }

        protected virtual void DeletedNotification(object sender, ItemDeletedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            Assert.IsNotNull(args.Item, "Deleted item in DeletedNotification args.");
            ItemUri itemUri = ItemUri.Parse(this.CurrentItemUri);
            Assert.IsNotNull(itemUri, "uri");
            Item item = args.Item.Database.GetItem(args.ParentID, itemUri.Language);
            if (item == null)
            {
                return;
            }
            if (itemUri.ItemID == args.Item.ID && itemUri.DatabaseName == args.Item.Database.Name)
            {
                this.currentItemDeleted = true;
                this.Redirect(WebEditRibbonForm.GetTarget(item));
                return;
            }
            if (Database.GetItem(itemUri) == null)
            {
                return;
            }
            Context.ClientPage.Start(this, "ConfirmAndReload");
        }

        protected virtual Item GetCurrentItem(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            string text = message["id"];
            if (string.IsNullOrEmpty(this.CurrentItemUri))
            {
                return null;
            }
            ItemUri itemUri = ItemUri.Parse(this.CurrentItemUri);
            if (itemUri == null)
            {
                return null;
            }
            Item item = Database.GetItem(itemUri);
            if (!string.IsNullOrEmpty(text) && item != null)
            {
                return item.Database.GetItem(text, item.Language);
            }
            return item;
        }

        protected virtual bool IsSimpleUser()
        {
            return false;
        }

        protected virtual void MovedNotification(object sender, ItemMovedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            string currentItemUri = this.CurrentItemUri;
            if (string.IsNullOrEmpty(currentItemUri))
            {
                return;
            }
            ItemUri itemUri = ItemUri.Parse(currentItemUri);
            if (itemUri == null)
            {
                return;
            }
            if (!(args.Item.ID == itemUri.ItemID) || !(args.Item.Database.Name == itemUri.DatabaseName))
            {
                Context.ClientPage.Start(this, "Reload");
                return;
            }
            Item item = Database.GetItem(itemUri);
            if (item == null)
            {
                Log.SingleError("Item not found after moving. Item uri:" + itemUri, this);
                return;
            }
            this.Redirect(item);
            WebEditRibbonForm.DisableOtherNotifications();
        }

        protected override void OnLoad(System.EventArgs e)
        {
            Assert.ArgumentNotNull(e, "e");
            base.OnLoad(e);
            this.refreshHasBeenAsked = false;
            SiteContext site = Context.Site;
            if (site != null)
            {
                site.Notifications.ItemDeleted += new ItemDeletedDelegate(this.DeletedNotification);
                site.Notifications.ItemMoved += new ItemMovedDelegate(this.MovedNotification);
                site.Notifications.ItemRenamed += new ItemRenamedDelegate(this.RenamedNotification);
                site.Notifications.ItemCopied += new ItemCopiedDelegate(this.CopiedNotification);
                site.Notifications.ItemCreated += new ItemCreatedDelegate(this.CreatedNotification);
                site.Notifications.ItemSaved += new ItemSavedDelegate(this.SavedNotification);
            }
            ItemUri itemUri = ItemUri.ParseQueryString();
            Assert.IsNotNull(itemUri, typeof(ItemUri));
            this.CurrentItemUri = itemUri.ToString();
            if (Context.ClientPage.IsEvent)
            {
                string currentItemUri = this.CurrentItemUri;
                if (!string.IsNullOrEmpty(currentItemUri))
                {
                    Item item;
                    using (new SecurityDisabler())
                    {
                        item = Database.GetItem(new ItemUri(currentItemUri));
                    }
                    if (item == null)
                    {
                        SheerResponse.Eval("scShowItemDeletedNotification(\"" + Translate.Text("The item does not exist. It may have been deleted by another user.") + "\")");
                        return;
                    }
                    if (Database.GetItem(new ItemUri(currentItemUri)) == null)
                    {
                        SheerResponse.Eval("scShowItemDeletedNotification(\"" + Translate.Text("The item could not be found.\n\nYou may not have read access or it may have been deleted by another user.").Replace('\n', ' ') + "\")");
                    }
                }
                return;
            }
            Item item2 = Database.GetItem(itemUri);
            if (item2 == null)
            {
                WebUtil.RedirectToErrorPage(Translate.Text("The item could not be found.\n\nYou may not have read access or it may have been deleted by another user."));
                return;
            }
            this.RenderRibbon(item2);
            this.RenderTreecrumb(item2);
            this.RenderNotifications(item2);
            this.RibbonForm.Attributes["class"] = UIUtil.GetBrowserClassString();
        }

        protected void Redirect(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            string text = args.Parameters["url"];
            Assert.IsNotNullOrEmpty(text, "url");
            SheerResponse.Eval(string.Format("window.parent.location.href='{0}'", text));
        }

        protected void Reload(ClientPipelineArgs args)
        {
            Assert.ArgumentNotNull(args, "args");
            SheerResponse.Eval("window.parent.location.reload(true)");
        }

        protected virtual void RenamedNotification(object sender, ItemRenamedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            ItemUri itemUri = ItemUri.Parse(this.CurrentItemUri);
            Assert.IsNotNull(itemUri, "uri");
            if (itemUri.ItemID == args.Item.ID && itemUri.DatabaseName == args.Item.Database.Name)
            {
                Item item = args.Item.Database.GetItem(args.Item.ID, itemUri.Language);
                if (item != null)
                {
                    this.Redirect(item);
                    return;
                }
            }
            Context.ClientPage.Start(this, "Reload");
        }

        protected virtual void RenderRibbon(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string queryString = WebUtil.GetQueryString("mode");
            string queryString2 = WebUtil.GetQueryString("sc_speakribbon");
            if (queryString2 == "1")
            {
                return;
            }
            Ribbon ribbon = new Ribbon
            {
                ID = "Ribbon",
                ShowContextualTabs = false,
                ActiveStrip = ((queryString == "preview") ? "VersionStrip" : WebUtil.GetCookieValue("sitecore_webedit_activestrip"))
            };
            string a;
            string path;
            if ((a = queryString) != null)
            {
                if (a == "preview")
                {
                    path = "/sitecore/content/Applications/WebEdit/Ribbons/Preview";
                    goto IL_BB;
                }
                if (a == "edit")
                {
                    path = (this.IsSimpleUser() ? "/sitecore/content/Applications/WebEdit/Ribbons/Simple" : "/sitecore/content/Applications/WebEdit/Ribbons/WebEdit");
                    goto IL_BB;
                }
            }
            path = "/sitecore/content/Applications/WebEdit/Ribbons/Debug";
            IL_BB:
            SiteRequest request = Context.Request;
            Assert.IsNotNull(request, "Site request not found.");
            CommandContext commandContext = new CommandContext(item);
            commandContext.Parameters["sc_pagesite"] = request.QueryString["sc_pagesite"];
            ribbon.CommandContext = commandContext;
            commandContext.RibbonSourceUri = new ItemUri(path, Context.Database);
            if (this.RibbonPane != null)
            {
                this.RibbonPane.InnerHtml = HtmlUtil.RenderControl(ribbon);
            }
        }

        protected virtual void RenderTreecrumb(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            if (this.IsSimpleUser() || WebUtil.GetQueryString("debug") == "1")
            {
                this.Treecrumb.Visible = false;
                return;
            }
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new System.IO.StringWriter());
            this.RenderTreecrumb(htmlTextWriter, item);
            this.RenderTreecrumbGo(htmlTextWriter, item);
            if (WebUtil.GetQueryString("mode") != "preview")
            {
                this.RenderTreecrumbEdit(htmlTextWriter, item);
            }
            if (this.Treecrumb != null)
            {
                this.Treecrumb.InnerHtml = htmlTextWriter.InnerWriter.ToString();
            }
        }

        protected virtual void RenderTreecrumb(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Item parent = item.Parent;
            if (parent != null && parent.ID != ItemIDs.RootID)
            {
                this.RenderTreecrumb(output, parent);
            }
            this.RenderTreecrumbLabel(output, item);
            this.RenderTreecrumbGlyph(output, item);
        }

        protected virtual void RenderTreecrumbEdit(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            bool flag = ItemUtility.CanEditItem(item, "webedit:open");
            if (flag)
            {
                CommandBuilder commandBuilder = new CommandBuilder("webedit:open");
                commandBuilder.Add("id", item.ID.ToString());
                string clientEvent = Context.ClientPage.GetClientEvent(commandBuilder.ToString());
                output.Write("<a href=\"javascript:void(0)\" onclick=\"{0}\" class=\"scTreecrumbGo\">", clientEvent);
            }
            else
            {
                output.Write("<span class=\"scTreecrumbGo\">");
            }
            ImageBuilder arg = new ImageBuilder
            {
                Src = "ApplicationsV2/16x16/edit.png",
                Class = "scTreecrumbGoIcon",
                Disabled = !flag
            };
            output.Write("{0} {1}{2}", arg, Translate.Text("Edit"), flag ? "</a>" : "</span>");
        }

        protected virtual void RenderTreecrumbGlyph(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            if (!item.HasChildren)
            {
                return;
            }
            if (Context.Device == null)
            {
                return;
            }
            DataContext dataContext = new DataContext
            {
                DataViewName = "Master"
            };
            ItemCollection children = dataContext.GetChildren(item);
            if (children == null || children.Count == 0)
            {
                return;
            }
            ShortID arg = ID.NewID.ToShortID();
            string arg2 = string.Format("javascript:scContent.showOutOfFrameGallery(this, event, \"Gallery.ItemChildren\", {{height: 30, width: 30 }}, {{itemuri: \"{0}\" }});", item.Uri);
            ImageBuilder arg3 = new ImageBuilder
            {
                Src = "/sitecore/shell/client/Speak/Assets/img/Speak/Common/16x16/dark_gray/separator.png",
                Class = "scTreecrumbChevronGlyph"
            };
            output.Write("<a id=\"L{0}\" class=\"scTreecrumbChevron\" href=\"#\" onclick='{1}'>{2}</a>", arg, arg2, arg3);
        }

        protected virtual void RenderTreecrumbGo(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            output.Write("<div class=\"scTreecrumbDivider\">{0}</div>", Images.GetSpacer(1, 1));
            bool flag = HasPresentationPipeline.Run(item);
            if (flag)
            {
                output.Write("<a href=\"{0}\" class=\"scTreecrumbGo\" target=\"_parent\">", Sitecore.Web.WebEditUtil.GetItemUrl(item));
            }
            else
            {
                output.Write("<span class=\"scTreecrumbGo\">");
            }
            ImageBuilder arg = new ImageBuilder
            {
                Src = "ApplicationsV2/16x16/arrow_right_green.png",
                Class = "scTreecrumbGoIcon",
                Disabled = !flag
            };
            output.Write("{0} {1}{2}", arg, Translate.Text("Go"), flag ? "</a>" : "</span>");
        }

        protected virtual void RenderTreecrumbLabel(HtmlTextWriter output, Item item)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(item, "item");
            Item parent = item.Parent;
            if (parent == null || parent.ID == ItemIDs.RootID)
            {
                return;
            }
            string arg = string.Format("javascript:scForm.postRequest(\"\",\"\",\"\",{0})", StringUtil.EscapeJavascriptString(string.Format("Update(\"{0}\")", item.Uri)));
            output.Write("<a class=\"scTreecrumbNode\" href=\"#\" onclick='{0}'>", arg);
            string text = "scTreecrumbNodeLabel";
            if (item.Uri.ToString() == this.CurrentItemUri)
            {
                text += " scTreecrumbNodeCurrentItem";
            }
            output.Write("<span class=\"{0}\">{1}</span></a>", text, item.DisplayName);
        }

        protected virtual void SavedNotification(object sender, ItemSavedEventArgs args)
        {
            Assert.ArgumentNotNull(sender, "sender");
            Assert.ArgumentNotNull(args, "args");
            if (!Context.PageDesigner.IsDesigning && !args.Changes.Renamed)
            {
                Context.ClientPage.Start(this, "Reload");
            }
        }

        protected void Update(string uri)
        {
            ItemUri itemUri = string.IsNullOrEmpty(uri) ? ItemUri.ParseQueryString() : ItemUri.Parse(uri);
            if (itemUri == null)
            {
                return;
            }
            this.ContextUri = itemUri.ToString();
            Item item = Database.GetItem(itemUri);
            if (item == null || this.CurrentItemUri == null)
            {
                return;
            }
            ItemUri itemUri2 = ItemUri.Parse(this.CurrentItemUri);
            if (itemUri2 == null)
            {
                return;
            }
            Item item2 = Database.GetItem(itemUri2);
            if (item2 == null)
            {
                return;
            }
            this.RenderRibbon(item2);
            this.RenderTreecrumb(item);
            SheerResponse.Eval("scAdjustPositioning()");
        }

        protected virtual bool VerifyWebeditLoaded()
        {
            return !string.IsNullOrEmpty(this.ContextUri);
        }

        private static void DisableOtherNotifications()
        {
            SiteContext site = Context.Site;
            if (site != null)
            {
                site.Notifications.Disabled = true;
            }
        }

        private static string GetNotificationIcon(PageEditorNotificationType notificationType)
        {
            switch (notificationType)
            {
                case PageEditorNotificationType.Error:
                    return "Custom/16x16/error.png";
                case PageEditorNotificationType.Information:
                    return "Custom/16x16/info.png";
                default:
                    return "Custom/16x16/warning.png";
            }
        }

        private static Item GetTarget(Item parent)
        {
            Assert.ArgumentNotNull(parent, "parent");
            if (HasPresentationPipeline.Run(parent))
            {
                return parent;
            }
            string siteName = Sitecore.Web.WebEditUtil.SiteName;
            SiteContext site = SiteContext.GetSite(siteName);
            if (site == null)
            {
                return parent;
            }
            string contentStartPath = site.ContentStartPath;
            Item item = parent.Database.GetItem(contentStartPath, parent.Language);
            return item ?? parent;
        }

        private static void RenderNotification(HtmlTextWriter output, PageEditorNotification notification, string marker)
        {
            Assert.ArgumentNotNull(output, "output");
            Assert.ArgumentNotNull(notification, "notification");
            Assert.ArgumentNotNull(marker, "marker");
            string arg = Themes.MapTheme(notification.Icon ?? WebEditRibbonForm.GetNotificationIcon(notification.Type));
            output.Write("<div class=\"scPageEditorNotification {0}{1}\">", notification.Type, marker);
            output.Write("<img class=\"Icon\" src=\"{0}\"/>", arg);
            output.Write("<div class=\"Description\">{0}</div>", notification.Description);
            System.Collections.Generic.List<PageEditorNotificationOption> options = notification.Options;
            foreach (PageEditorNotificationOption current in options)
            {
                output.Write("<a onclick=\"javascript: return scForm.postEvent(this, event, '{0}')\" href=\"#\" class=\"OptionTitle\">{1}</a>", current.Command, current.Title);
            }
            output.Write("<br style=\"clear: both\"/>");
            output.Write("</div>");
        }

        private void Redirect(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            string itemUrl = Sitecore.Web.WebEditUtil.GetItemUrl(item);
            Context.ClientPage.Start(this, "Redirect", new NameValueCollection
            {
                {
                    "url",
                    itemUrl
                }
            });
        }

        private void RenderNotifications(Item item)
        {
            Assert.ArgumentNotNull(item, "item");
            if (WebUtil.GetQueryString("mode") != "edit")
            {
                return;
            }
            System.Collections.Generic.List<PageEditorNotification> pageEditorNotifications = (System.Collections.Generic.List<PageEditorNotification>)ItemUtility.GetPageEditorNotifications(item);
            if (pageEditorNotifications.Count == 0)
            {
                this.Notifications.Visible = false;
                return;
            }
            HtmlTextWriter htmlTextWriter = new HtmlTextWriter(new System.IO.StringWriter());
            int count = pageEditorNotifications.Count;
            for (int i = 0; i < count; i++)
            {
                PageEditorNotification notification = pageEditorNotifications[i];
                string text = string.Empty;
                if (i == 0)
                {
                    text += " First";
                }
                if (i == count - 1)
                {
                    text += " Last";
                }
                WebEditRibbonForm.RenderNotification(htmlTextWriter, notification, text);
            }
            this.Notifications.InnerHtml = htmlTextWriter.InnerWriter.ToString();
        }
    }
}