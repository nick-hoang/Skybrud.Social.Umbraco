<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="InstagramOAuth.aspx.cs" Inherits="Skybrud.Social.Umbraco.App_Plugins.Skybrud.Social.Dialogs.InstagramOAuth" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Instagram OAuth</title>
    <link href="https://fonts.googleapis.com/css?family=Open+Sans:400,700" rel="stylesheet" type="text/css">
    <style>
        body {
            margin: 0;
            font-family: 'Open Sans', 'Helvetica Neue', Helvetica, Arial, sans-serif;
            font-size: 12px;
        }
        .umb-panel-header {
            height: 99px;
            background: #f8f8f8;
            border-bottom: 1px solid #d9d9d9;
            padding: 0 20px;
            line-height: 99px;
        }
        h1 {
            margin: 0;
            line-height: 99px;
            font-size: 18px;
            font-weight: normal;
        }
        .content {
            padding: 20px;
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
    <div>
        <div class="umb-panel-header">
            <h1>Instagram OAuth</h1>
        </div>
        <div class="content">
            <div>
                <asp:Literal runat="server" ID="Content" /> 
            </div>
            <div style="display: none">
                <asp:Literal runat="server" ID="DebugInfo" />
            </div>
            <asp:Panel runat="server" ID="pnlInstagramPageName" Visible="false">
                Please enter your Facebook page name that is associated with your Instagram account. Page names can be found on your Facebook page as per the below screenshot example.<br />
                <a href="../images/codebrewery_easybrew_instagram_business_oauth_sample.png" target="_blank">
                    <img src="../images/codebrewery_easybrew_instagram_business_oauth_sample.png" alt="" style="width: 300px;" />
                </a>
                <br /><br />
                <asp:Textbox runat="server" ID="txtInstagramPageName"></asp:Textbox> 
                <asp:RequiredFieldValidator runat="server" id="reqName" controltovalidate="txtInstagramPageName" errormessage="Please enter your facebook page name!" ForeColor="red" />
                <br /><br />
                <asp:Button runat="server" ID="btnInstagramPageNameSubmit" Text="Continue" OnClick="btnInstagramPageNameSubmit_Click" />
            </asp:Panel>
        </div>
    </div>
    </form>
</body>
</html>
