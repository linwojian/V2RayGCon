﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using V2RayGCon.Resource.Resx;

namespace V2RayGCon.Service
{
    class Notifier :
        Model.BaseClass.SingletonService<Notifier>,
        VgcApis.Models.IServices.INotifierService
    {
        NotifyIcon ni;
        Setting setting;
        Servers servers;
        ShareLinkMgr slinkMgr;
        Bitmap orgIcon = null;

        VgcApis.Libs.Tasks.LazyGuy notifierUpdater;

        Notifier()
        {
            notifierUpdater = new VgcApis.Libs.Tasks.LazyGuy(
                RefreshNotifyIconNow,
                VgcApis.Models.Consts.Intervals.NotifierTextUpdateIntreval);
        }

        public void Run(
            Setting setting,
            Servers servers,
            ShareLinkMgr shareLinkMgr)
        {
            this.setting = setting;
            this.servers = servers;
            this.slinkMgr = shareLinkMgr;

            CreateNotifyIcon();

            servers.OnRequireNotifyTextUpdate +=
                OnRequireNotifyTextUpdateHandler;


            ni.MouseClick += (s, a) =>
            {
                if (a.Button != MouseButtons.Left)
                {
                    return;
                }

                // https://stackoverflow.com/questions/2208690/invoke-notifyicons-context-menu
                // MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
                // mi.Invoke(ni, null);

                Views.WinForms.FormMain.GetForm()?.Show();
            };

            notifierUpdater.DoItLater();
        }

        #region public method
        public void RefreshNotifyIcon() =>
            notifierUpdater.DoItLater();

        public void ScanQrcode()
        {
            void Success(string link)
            {
                // no comment ^v^
                if (link == StrConst.Nobody3uVideoUrl)
                {
                    Lib.UI.VisitUrl(I18N.VisitWebPage, StrConst.Nobody3uVideoUrl);
                    return;
                }

                var msg = Lib.Utils.CutStr(link, 90);
                setting.SendLog($"QRCode: {msg}");
                slinkMgr.ImportLinkWithOutV2cfgLinks(link);
            }

            void Fail()
            {
                MessageBox.Show(I18N.NoQRCode);
            }

            Lib.QRCode.QRCode.ScanQRCode(Success, Fail);
        }

        public void RunInUiThread(Action updater) =>
            VgcApis.Libs.UI.RunInUiThread(ni.ContextMenuStrip, updater);

#if DEBUG
        public void InjectDebugMenuItem(ToolStripMenuItem menu)
        {
            ni.ContextMenuStrip.Items.Insert(0, new ToolStripSeparator());
            ni.ContextMenuStrip.Items.Insert(0, menu);
        }
#endif

        ToolStripMenuItem oldPluginMenu = null;
        /// <summary>
        /// null means delete menu
        /// </summary>
        /// <param name="pluginMenu"></param>
        public void UpdatePluginMenu(ToolStripMenuItem pluginMenu)
        {
            RemoveOldPluginMenu();
            if (pluginMenu == null)
            {
                return;
            }

            oldPluginMenu = pluginMenu;
            RunInUiThread(
                () => ni.ContextMenuStrip.Items.Insert(
                    2, oldPluginMenu));
        }

        #endregion

        #region private method
        void RefreshNotifyIconNow()
        {
            var list = servers.GetAllServersOrderByIndex()
                .Where(s => s.GetCoreCtrl().IsCoreRunning())
                .ToList();

            RefreshNotifyIconText(list);

            var isRunning = list.Any();


            RefreshNotifyIconImage(isRunning);
        }

        private void RefreshNotifyIconImage(bool isRunning)
        {
            var icon = orgIcon.Clone() as Bitmap;
            var size = icon.Size;

            using (Graphics g = Graphics.FromImage(icon))
            {
                g.InterpolationMode = InterpolationMode.High;
                g.CompositingQuality = CompositingQuality.HighQuality;

                DrawProxyModeCornerCircle(g, size);
                DrawIsRunningCornerMark(g, size, isRunning);
            }

            ni.Icon?.Dispose();
            ni.Icon = Icon.FromHandle(icon.GetHicon());
        }

        void DrawProxyModeCornerCircle(
            Graphics graphics, Size size)
        {
            Brush br;

            switch (ProxySetter.Lib.Sys.WinInet.GetProxySettings().proxyMode)
            {
                case (int)ProxySetter.Lib.Sys.WinInet.ProxyModes.PAC:
                    br = Brushes.DeepPink;
                    break;
                case (int)ProxySetter.Lib.Sys.WinInet.ProxyModes.Proxy:
                    br = Brushes.Red;
                    break;
                default:
                    br = Brushes.ForestGreen;
                    break;
            }

            var w = size.Width;
            var x = w * 0.4f;
            var s = w * 0.6f;
            graphics.FillEllipse(br, x, x, s, s);
        }

        private void DrawIsRunningCornerMark(
            Graphics graphics, Size size, bool isRunning)
        {
            var w = size.Width;
            var cx = w * 0.72f;

            if (isRunning)
            {
                var cr = w * 0.22f;
                var dh = Math.Sqrt(3) * cr / 2f;

                var tri = new Point[] {
                    new Point((int)(cx - cr / 2f),(int)(cx - dh)),
                    new Point((int)(cx + cr),(int)cx),
                    new Point((int)(cx - cr / 2f),(int)(cx + dh)),
                };

                graphics.FillPolygon(Brushes.White, tri);
            }
            else
            {
                var rw = w * 0.44f;
                var rh = w * 0.14f;
                var rect = new Rectangle((int)(cx - rw / 2f), (int)(cx - rh / 2f), (int)rw, (int)rh);
                graphics.FillRectangle(Brushes.White, rect);
            }
        }

        private void RemoveOldPluginMenu()
        {
            if (this.oldPluginMenu == null)
            {
                return;
            }
            RunInUiThread(
                () => ni.ContextMenuStrip.Items.Remove(
                    this.oldPluginMenu));
        }

        void OnRequireNotifyTextUpdateHandler(object sender, EventArgs args) =>
            notifierUpdater.DoItLater();

        string GetterSysProxyInfo()
        {
            var proxySetting = ProxySetter.Lib.Sys.ProxySetter.GetProxySetting();

            switch (proxySetting.proxyMode)
            {
                case (int)ProxySetter.Lib.Sys.WinInet.ProxyModes.PAC:
                    return proxySetting.pacUrl;
                case (int)ProxySetter.Lib.Sys.WinInet.ProxyModes.Proxy:
                    return proxySetting.proxyAddr;
            }
            return null;
        }

        void RefreshNotifyIconText(
            List<VgcApis.Models.Interfaces.ICoreServCtrl> list)
        {
            var count = list.Count;

            var texts = new List<string>();

            void done()
            {
                var sysProxyInfo = GetterSysProxyInfo();
                if (!string.IsNullOrEmpty(sysProxyInfo))
                {
                    texts.Add(I18N.CurSysProxy + Lib.Utils.CutStr(sysProxyInfo, 50));
                }
                SetNotifyText(string.Join(Environment.NewLine, texts));
                return;
            }

            if (count <= 0 || count > 2)
            {
                texts.Add(count <= 0 ?
                    I18N.Description :
                    count.ToString() + I18N.ServersAreRunning);

                done();
                return;
            }

            void worker(int index, Action next)
            {
                list[index].GetConfiger().GetterInfoForNotifyIconf(s =>
                {
                    texts.Add(s);
                    next?.Invoke();
                });
            }

            Lib.Utils.ChainActionHelperAsync(count, worker, done);
        }

        private void SetNotifyText(string rawText)
        {
            var text = string.IsNullOrEmpty(rawText) ?
                I18N.Description :
                Lib.Utils.CutStr(rawText, 127);

            if (ni.Text == text)
            {
                return;
            }

            // https://stackoverflow.com/questions/579665/how-can-i-show-a-systray-tooltip-longer-than-63-chars
            Type t = typeof(NotifyIcon);
            BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
            t.GetField("text", hidden).SetValue(ni, text);
            if ((bool)t.GetField("added", hidden).GetValue(ni))
                t.GetMethod("UpdateIcon", hidden).Invoke(ni, new object[] { true });
        }

        void CreateNotifyIcon()
        {
            ni = new NotifyIcon
            {
                Text = I18N.Description,
                Icon = VgcApis.Libs.UI.GetAppIcon(),
                BalloonTipTitle = Properties.Resources.AppName,

                ContextMenuStrip = CreateMenu(),
                Visible = true
            };

            orgIcon = ni.Icon.ToBitmap();
        }

        ContextMenuStrip CreateMenu()
        {
            var menu = new ContextMenuStrip();

            var factor = Lib.UI.GetScreenScalingFactor();
            if (factor > 1)
            {
                menu.ImageScalingSize = new System.Drawing.Size(
                    (int)(menu.ImageScalingSize.Width * factor),
                    (int)(menu.ImageScalingSize.Height * factor));
            }

            menu.Items.AddRange(new ToolStripMenuItem[] {
                new ToolStripMenuItem(
                    I18N.MainWin,
                    Properties.Resources.WindowsForm_16x,
                    (s,a)=> Views.WinForms.FormMain.ShowForm()),

                new ToolStripMenuItem(
                    I18N.OtherWin,
                    Properties.Resources.CPPWin32Project_16x,
                    new ToolStripMenuItem[]{
                        new ToolStripMenuItem(
                            I18N.ConfigEditor,
                            Properties.Resources.EditWindow_16x,
                            (s,a)=>new Views.WinForms.FormConfiger() ),
                        new ToolStripMenuItem(
                            I18N.GenQRCode,
                            Properties.Resources.AzureVirtualMachineExtension_16x,
                            (s,a)=> Views.WinForms.FormQRCode.ShowForm()),
                        new ToolStripMenuItem(
                            I18N.Log,
                            Properties.Resources.FSInteractiveWindow_16x,
                            (s,a)=> Views.WinForms.FormLog.ShowForm() ),
                        new ToolStripMenuItem(
                            I18N.Options,
                            Properties.Resources.Settings_16x,
                            (s,a)=> Views.WinForms.FormOption.ShowForm() ),
                    }),

                new ToolStripMenuItem(
                    I18N.ScanQRCode,
                    Properties.Resources.ExpandScope_16x,
                    (s,a)=> ScanQrcode()                    ),

                new ToolStripMenuItem(
                    I18N.ImportLink,
                    Properties.Resources.CopyLongTextToClipboard_16x,
                    (s,a)=>{
                        string links = Lib.Utils.GetClipboardText();
                        slinkMgr.ImportLinkWithOutV2cfgLinks(links);
                    }),

                new ToolStripMenuItem(
                    I18N.DownloadV2rayCore,
                    Properties.Resources.ASX_TransferDownload_blue_16x,
                    (s,a)=> Views.WinForms.FormDownloadCore.GetForm()),
            });

            menu.Items.Add(new ToolStripSeparator());

            menu.Items.AddRange(new ToolStripMenuItem[] {
                new ToolStripMenuItem(
                    I18N.About,
                    Properties.Resources.StatusHelp_16x,
                    (s,a)=>Lib.UI.VisitUrl(
                        I18N.VistPorjectPage,
                        Properties.Resources.ProjectLink)),

                new ToolStripMenuItem(
                    I18N.Exit,
                    Properties.Resources.CloseSolution_16x,
                    (s,a)=>{
                        if (Lib.UI.Confirm(I18N.ConfirmExitApp)){
                            Application.Exit();
                        }
                    })
            });

            return menu;
        }


        #endregion

        #region protected methods
        protected override void Cleanup()
        {
            ni.Visible = false;

            servers.OnRequireNotifyTextUpdate -=
                OnRequireNotifyTextUpdateHandler;

            notifierUpdater.Quit();
        }

        #endregion
    }
}
