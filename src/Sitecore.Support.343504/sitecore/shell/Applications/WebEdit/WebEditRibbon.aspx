<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="WebEditRibbon.aspx.cs" Inherits="Sitecore.Shell.Applications.WebEdit.WebEditRibbon" %>
<%@ Register TagPrefix="sc" Namespace="Sitecore.Web.UI.HtmlControls" Assembly="Sitecore.Kernel" %>
<sc:DocumentType runat="server" ID="docType"></sc:DocumentType>
<html>
<head runat="server">
  <title>Sitecore</title>
</head>
<body onload="javascript:scOnLoad();">
  <input type="hidden" id="scActiveRibbonStrip" name="scActiveRibbonStrip" />
  <input type="hidden" id="scHtmlValue" name="scHtmlValue" />
  <input type="hidden" id="scPlainValue" name="scPlainValue" />
  <input type="hidden" id="scLayoutDefinition" name="scLayoutDefinition" />

  <sc:CodeBeside runat="server" Type="Sitecore.Shell.Applications.WebEdit.WebEditRibbonForm,Sitecore.ExperienceEditor" />

  <form id="RibbonForm" runat="server">
    <input id="__FRAMENAME" type="hidden" value="Shell" />
    <input id="__SAVEBUTTONSTATE" type="hidden" value="" />
  </form>
</body>
</html>
