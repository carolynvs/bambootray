﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BambooTray.App.ModelBuilders;
using BambooTray.App.Models;
using BambooTray.App.Properties;
using BambooTray.Domain.Settings;
using BambooTray.Services;

namespace BambooTray.App
{
    /// <summary>
    /// Main Window
    /// </summary>
    public partial class MainWindow : Form
    {
        private readonly SettingsService _settingsService;

        private readonly List<Icon> _buildingIcons;

        private int _currentBuildIcon;
        
        private bool _applicationIsExiting;

        private List<MainViewModel> _lastBuildData;

        private enum IconEnum
        {
            Grey,
            Red,
            Green,
            Blue,
            Yellow1,
            Yellow2,
            Yellow3,
            Yellow4
        };

        private Dictionary<IconEnum, Icon> _statusIcons = new Dictionary<IconEnum, Icon>();  

        public MainWindow(SettingsService settingsService)
        {
            InitializeComponent();
            _settingsService = settingsService;

            _buildingIcons = new List<Icon>();
            _buildingIcons = GetBuildingIcons(4);

            _statusIcons = new Dictionary<IconEnum, Icon>
            {
                {IconEnum.Grey, Icon.FromHandle(Resources.BambooGrey.GetHicon())},
                {IconEnum.Red, Icon.FromHandle(Resources.BambooRed.GetHicon())},
                {IconEnum.Green, Icon.FromHandle(Resources.BambooGreen.GetHicon())},
            };

            notifyIcon.Icon = _statusIcons[IconEnum.Grey];

            _lastBuildData = new List<MainViewModel>();
            buildsListView.SmallImageList = GetListViewImages();
            updateTimer.Interval = Settings.PollTime;
            RefreshBuilds();
        }

        private TraySettings Settings
        {
            get
            {
                return _settingsService.TraySettings;
            }
        }

        private static ImageList GetListViewImages()
        {
            var imageList = new ImageList();
            imageList.Images.Add("Successful", Resources.BambooGreen);
            imageList.Images.Add("Failed", Resources.BambooRed);
            imageList.Images.Add("Building", Resources.BambooYellow1);
            imageList.Images.Add("Offline", Resources.BambooGrey);
            return imageList;
        }

        private static List<Icon> GetBuildingIcons(int numberOfIcons)
        {
            var icons = new List<Icon>();
            for (var i = 1; i < numberOfIcons; i++)
            {
                var bitmap = Resources.ResourceManager.GetObject("BambooYellow" + i) as Bitmap;
                if (bitmap != null)
                {
                    icons.Add(Icon.FromHandle(bitmap.GetHicon()));
                }
            }

            return icons;
        }

        private void RefreshServerBuild(Server server, List<MainViewModel> plans)
        {
            // todo: should be able to populate per server, but I believe this will only handle 1 server
            try
            {
                var bambooService = new BambooService(new Uri(server.Address), server.Username, server.PlaintextPassword);

                foreach (var buildPlan in server.BuildPlans)
                {
                    var planDetail = bambooService.GetPlanDetail(buildPlan.Key);
                    planDetail.Results = bambooService.GetPlanResults(buildPlan.Key);
                    var resultDetail = planDetail.Results.FirstOrDefault();
                    if (resultDetail != null)
                    {
                        var firstOrDefault = planDetail.Results.FirstOrDefault();
                        if (firstOrDefault != null)
                            firstOrDefault.Detail = bambooService.GetResultDetail(resultDetail.Key);
                    }

                    plans.Add(MainViewModelBuilder.Build(planDetail, server));
                }

                GetPlansListViewData(plans);
                DoNotifications(plans);
                UpdateTrayIcon(plans);
                _lastBuildData = plans;
            }
            catch (BambooRequestException)
            {
                notifyIcon.Icon = _statusIcons[IconEnum.Grey];
                foreach (ListViewItem item in buildsListView.Items)
                {
                    item.ImageKey = "Offline";
                }
            }
        }

        private void RefreshBuilds()
        {
            var plans = new List<MainViewModel>();

            buildsListView.Items.Clear();
            foreach (var server in Settings.Servers.Where(server => server.BuildPlans.Count > 0))
                RefreshServerBuild(server, plans);
        }

        private void UpdateTrayIcon(IEnumerable<MainViewModel> currentBuildData)
        {
            var building = false;
            var broken = false;
            foreach (var plan in currentBuildData)
            {
                if (plan.BuildActive)
                {
                    building = true;
                }

                if (plan.BuildBroken)
                {
                    broken = true;
                }
            }

            iconTimer.Enabled = building;

            notifyIcon.Icon = broken
                                       ? _statusIcons[IconEnum.Red]
                                       : _statusIcons[IconEnum.Green];
        }

        private void DoNotifications(IEnumerable<MainViewModel> currentBuildData)
        {
            foreach (var currentBuild in currentBuildData)
            {
                var lastBuild = _lastBuildData.FirstOrDefault(x => x.PlanKey == currentBuild.PlanKey);
                if (lastBuild != null)
                {
                    if (lastBuild.BuildActive && !currentBuild.BuildActive)
                    {
                        // Build Status has just changed... 
                        if (lastBuild.BuildBroken && !currentBuild.BuildBroken)
                        {
                            notifyIcon.ShowBalloonTip(
                                _settingsService.TraySettings.BalloonToolTipTimeOut,
                                string.Format("{0} {1}: Fixed!", currentBuild.ProjectName, currentBuild.PlanKey),
                                "Recent checkins have fixed the build.", 
                                ToolTipIcon.Info);
                        }
                        else if (!lastBuild.BuildBroken && currentBuild.BuildBroken)
                        {
                            notifyIcon.ShowBalloonTip(
                                _settingsService.TraySettings.BalloonToolTipTimeOut,
                                string.Format("{0} {1}: Broken!", currentBuild.ProjectName, currentBuild.PlanKey),
                                "Recent checkins have broken the build.",
                                ToolTipIcon.Error);
                        }
                        else if (!lastBuild.BuildBroken && !currentBuild.BuildBroken)
                        {
                            notifyIcon.ShowBalloonTip(
                                _settingsService.TraySettings.BalloonToolTipTimeOut,
                                string.Format("{0} {1}: Build Successful!", currentBuild.ProjectName, currentBuild.PlanKey),
                                "Yet another successful build.",
                                ToolTipIcon.Info);
                        }
                        else if (lastBuild.BuildBroken && currentBuild.BuildBroken)
                        {
                            notifyIcon.ShowBalloonTip(
                                _settingsService.TraySettings.BalloonToolTipTimeOut,
                                string.Format("{0} {1}: Broken!", currentBuild.ProjectName, currentBuild.PlanKey),
                                "The build is still broken.",
                                ToolTipIcon.Error);
                        }
                    }
                }
            }
        }

        private void GetPlansListViewData(IEnumerable<MainViewModel> currentBuildData)
        {
            buildsListView.Items.Clear();
            foreach (var p in currentBuildData)
            {
                var lv = new ListViewItem
                {
                    Text = p.ServerName,
                    ImageKey =
                        p.BuildActivity == "Building"
                            ? p.BuildActivity
                            : (string.IsNullOrEmpty(p.BuildStatus) ? "Offline" : p.BuildStatus)
                };
                lv.SubItems.Add(p.ProjectName);
                lv.SubItems.Add(p.PlanKey);
                lv.SubItems.Add(p.BuildActivity);
                lv.SubItems.Add(p.BuildStatus);
                lv.SubItems.Add(p.LastBuildTime);
                lv.SubItems.Add(p.LastBuildDuration);
                lv.SubItems.Add(p.LastBuildNumber);
                lv.SubItems.Add(p.LastVcsRevision);
                lv.SubItems.Add(p.SuccessfulTestCount);
                lv.SubItems.Add(p.FailedTestCount);
                buildsListView.Items.Add(lv);
            }
        }

        private void PreferencesToolStripMenuItemClick(object sender, EventArgs e)
        {
            // Open the Preferences Window
            var preferencesWindow = new PreferencesWindow(_settingsService);
            preferencesWindow.ShowDialog(this);
        }

        private void AboutToolStripMenuItemClick(object sender, EventArgs e)
        {
            // Open the about Window
            var aboutBox = new AboutWindow();
            aboutBox.ShowDialog(this);
        }

        private void ExitToolStripMenuItemClick(object sender, EventArgs e)
        {
            // User has clicked Exit or Keyed ALT+F4
            _applicationIsExiting = true;
            notifyIcon.Visible = false;
            Application.Exit();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_applicationIsExiting)
            {
                Hide();
                e.Cancel = true;
            }
        }

        private void NotifyIconClick(object sender, EventArgs e)
        {
            // When tray icon is clicked, show main window and bring to front
            Show();
            Activate();
            BringToFront();
        }

        private void BuildIconTimerTick(object sender, EventArgs e)
        {
            // This isn't very nice, but to animate the tray icon when a build is in progress.
            notifyIcon.Icon = _buildingIcons[_currentBuildIcon];
            
            _currentBuildIcon++;
            if (_currentBuildIcon == 3)
            {
                _currentBuildIcon = 0;
            }
        }

        private void UpdateTimerTick(object sender, EventArgs e)
        {
            RefreshBuilds();
        }
    }
}
