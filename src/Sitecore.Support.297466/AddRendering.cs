namespace Sitecore.Support.XA.Foundation.LocalDatasources.Commands
{
  using Microsoft.Extensions.DependencyInjection;
  using Sitecore;
  using Sitecore.Data;
  using Sitecore.Data.Items;
  using Sitecore.DependencyInjection;
  using Sitecore.Diagnostics;
  using Sitecore.Pipelines;
  using Sitecore.Pipelines.GetRenderingDatasource;
  using Sitecore.Shell.Framework.Commands;
  using Sitecore.Web;
  using Sitecore.Web.UI.Sheer;
  using Sitecore.XA.Foundation.LocalDatasources.Models;
  using Sitecore.XA.Foundation.LocalDatasources.Pipelines.CopyGlobalDatasource;
  using Sitecore.XA.Foundation.LocalDatasources.Pipelines.CreateAutoDatasource;
  using Sitecore.XA.Foundation.LocalDatasources.Services;
  using System;
  using System.Collections.Generic;
  using System.Collections.Specialized;

  [Serializable]
  public class AddRendering : Sitecore.XA.Foundation.LocalDatasources.Commands.AddRendering
  {
    public override void Execute(CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");
      NameValueCollection parameters = base.CreateParameters(context);
      Context.ClientPage.Start(this, "Run", parameters);
    }

    protected override void Run(ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");
      bool flag = !string.IsNullOrEmpty(args.Parameters["toolboxRendering"]);
      if (flag && !args.IsPostBack && ShortID.IsShortID(args.Parameters["toolboxRendering"]))
      {
        ID iD = ShortID.Parse(args.Parameters["toolboxRendering"]).ToID();
        Item item;
        if (!ID.IsNullOrEmpty(iD) && (item = (Context.ContentDatabase ?? Context.Database).GetItem(iD)) != null)
        {
          args.IsPostBack = true;
          args.Result = iD + ",," + item["Open Properties After Add"];
        }
      }
      if (args.IsPostBack)
      {
        if (args.HasResult)
        {
          if (!IsSelectDatasourceDialogPostBack(args))
          {
            string text = args.Parameters["PromptState"] ?? args.Result;
            string itemPath;
            bool flag2;
            if (text.IndexOf(',') >= 0)
            {
              string[] array = text.Split(',');
              itemPath = array[0];
              flag2 = (array[2] == "1");
            }
            else
            {
              itemPath = text;
              flag2 = false;
            }
            Item itemNotNull = Client.GetItemNotNull(itemPath);
            GetRenderingDatasourceArgs getRenderingDatasourceArgs = new GetRenderingDatasourceArgs(itemNotNull)
            {
              ContextItemPath = args.Parameters["contextitempath"],
              ContentLanguage = WebEditUtil.GetClientContentLanguage()
            };
            if (!IsMorphRenderingsRequest(args))
            {
              EditRenderingPropertiesParameters value = new EditRenderingPropertiesParameters
              {
                DeviceId = new ID(WebUtil.GetFormValue("scDeviceID")),
                Layout = args.Parameters["layout"],
                Placeholder = args.Parameters["placeholder"]
              };
              getRenderingDatasourceArgs.CustomData.Add("EditRenderingPropertiesParameters", value);
              CorePipeline.Run("getRenderingDatasource", getRenderingDatasourceArgs);
              string text2 = getRenderingDatasourceArgs.CustomData["SXA::LocalDataFolderParent"]?.ToString();
              if (text2 != null)
              {
                args.Parameters["SXA::LocalDataFolderParent"] = text2;
              }
            }
            if (ServiceLocator.ServiceProvider.GetService<ILocalDatasourceService>().IsAutoDatasourceRendering(itemNotNull) && getRenderingDatasourceArgs.Prototype != null && !IsMorphRenderingsRequest(args))
            {
              CreateAutoDatasourceArgs createAutoDatasourceArgs = new CreateAutoDatasourceArgs
              {
                ClientPipelineArgs = args,
                GetRenderingDatasourceArgs = getRenderingDatasourceArgs
              };
              CorePipeline.Run("createAutoDatasource", createAutoDatasourceArgs, false);
              if (createAutoDatasourceArgs.Aborted)
              {
                if (createAutoDatasourceArgs.ControlAddingCancelled)
                {
                  HandleResponse("'chrome:placeholder:controladdingcancelled'", "''", flag);
                }
              }
              else
              {
                string datasource = createAutoDatasourceArgs.Datasource;
                HandleResponse("'chrome:placeholder:controladded'", $"{{ id: '{itemNotNull.ID.ToShortID()}', openProperties: {flag2.ToString().ToLowerInvariant()}, dataSource: '{datasource}' }}", flag);
              }
            }
            else if (!string.IsNullOrEmpty(getRenderingDatasourceArgs.DialogUrl) && !IsMorphRenderingsRequest(args))
            {
              args.IsPostBack = false;
              args.Parameters["SelectedRendering"] = itemNotNull.ID.ToShortID().ToString();
              args.Parameters["OpenProperties"] = flag2.ToString().ToLowerInvariant();
              #region Modified code
              SheerResponse.ShowModalDialog(getRenderingDatasourceArgs.DialogUrl, "1200px", "700px", string.Empty, true);
              #endregion
              args.WaitForPostBack();
            }
            else
            {
              HandleResponse(IsMorphRenderingsRequest(args) ? "'chrome:rendering:morphcompleted'" : "'chrome:placeholder:controladded'", $"{{ id: '{itemNotNull.ID.ToShortID()}', openProperties: {flag2.ToString().ToLowerInvariant()} }}", flag);
            }
          }
          else
          {
            string text3 = args.Result;
            Item item2 = Client.ContentDatabase.GetItem(args.Parameters["contextitempath"], WebEditUtil.GetClientContentLanguage());
            Item item3 = Client.ContentDatabase.GetItem(args.Result, WebEditUtil.GetClientContentLanguage()) ?? Client.ContentDatabase.GetItem(args.Parameters["ConfirmationPrompt"] ?? string.Empty, WebEditUtil.GetClientContentLanguage());
            if (item2 != null && item3 != null)
            {
              CopyDatasourceArgs copyDatasourceArgs = new CopyDatasourceArgs
              {
                ClientPipelineArgs = args,
                ContextItem = item2,
                SelectedDatasource = item3
              };
              CorePipeline.Run("copyGlobalDatasource", copyDatasourceArgs, false);
              text3 = ((!string.IsNullOrWhiteSpace(copyDatasourceArgs.Datasource)) ? copyDatasourceArgs.Datasource : text3);
            }
            if (!string.IsNullOrWhiteSpace(text3))
            {
              HandleResponse("'chrome:placeholder:controladded'", string.Format("{{ id: '{0}', openProperties: {1}, dataSource: '{2}' }}", args.Parameters["SelectedRendering"] ?? string.Empty, args.Parameters["OpenProperties"] ?? "false", text3), flag);
            }
          }
        }
        else if (flag)
        {
          HandleResponse("'chrome:placeholder:controladdingcancelled'", "''", true);
        }
      }
      else
      {
        RunGetPlaceholderRenderingsPipeline(args.Parameters, out List<Item> _, out string dialogUrl);
        if (!string.IsNullOrEmpty(dialogUrl))
        {
          SheerResponse.ShowModalDialog(dialogUrl, "1175px", "605px", string.Empty, true);
          args.WaitForPostBack();
        }
      }
    }

  }
}