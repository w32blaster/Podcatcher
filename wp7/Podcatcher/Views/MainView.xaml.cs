﻿/**
 * Copyright (c) 2012, Johan Paul <johan@paul.fi>
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the <organization> nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using System.Collections.ObjectModel;
using Podcatcher.ViewModels;
using System.Diagnostics;
using System.Collections.Specialized;
using System.IO.IsolatedStorage;

namespace Podcatcher
{
    public partial class MainView : PhoneApplicationPage
    {
        private const int PODCAST_PLAYER_PIVOR_INDEX = 2;
        private const int TRIAL_SUBSCRIPTION_LIMIT   = 2;
        
        private PodcastSqlModel m_podcastsModel = PodcastSqlModel.getInstance();
        private PodcastEpisodesDownloadManager m_episodeDownloadManager = PodcastEpisodesDownloadManager.getInstance();
        private PodcastSubscriptionsManager m_subscriptionsManager;
        private PodcastPlayerControl m_playerControl;
        private IsolatedStorageSettings m_applicationSettings = null;

        public MainView()
        {
            InitializeComponent();

            // Upon startup, refresh all subscriptions so we get the latest episodes for each. 
            m_subscriptionsManager = PodcastSubscriptionsManager.getInstance();
            m_subscriptionsManager.OnPodcastSubscriptionsChanged += new SubscriptionManagerHandler(m_subscriptionsManager_OnPodcastSubscriptionsChanged);

            m_subscriptionsManager.refreshSubscriptions();

            // Post-pageinitialization event call hookup.

            // Hook to the event when the download list changes, so we can update the pivot header text for the 
            // download page. 
            ((INotifyCollectionChanged)EpisodeDownloadList.Items).CollectionChanged += downloadListChanged;

            // Hook to the event when the podcast player starts playing. 
            m_playerControl = PodcastPlayerControl.getIntance();
            m_playerControl.PodcastPlayerStarted += new EventHandler(PodcastPlayer_PodcastPlayerStarted);

            // Hook to SQL events.
            PodcastSqlModel.getInstance().OnPodcastSqlOperationChanged += new PodcastSqlModel.PodcastSqlHandler(MainView_OnPodcastSqlOperationChanged);

            m_applicationSettings = IsolatedStorageSettings.ApplicationSettings;
        }

        void MainView_OnPodcastSqlOperationChanged(object source, PodcastSqlModel.PodcastSqlHandlerArgs e)
        {
            if (e.operationStatus == PodcastSqlModel.PodcastSqlHandlerArgs.SqlOperation.DeleteSubscriptionStarted)
            {
                deleteProgressOverlay.Visibility = Visibility.Visible;
            }

            if (e.operationStatus == PodcastSqlModel.PodcastSqlHandlerArgs.SqlOperation.DeleteSubscriptionFinished)
            {
                deleteProgressOverlay.Visibility = Visibility.Collapsed;
            }
        }

        void m_subscriptionsManager_OnPodcastSubscriptionsChanged(object source, SubscriptionManagerArgs e)
        {
            if (e.state == PodcastSubscriptionsManager.SubscriptionsState.StartedRefreshing)
            {
                UpdatingIndicator.Visibility = Visibility.Visible;
            }

            if (e.state == PodcastSubscriptionsManager.SubscriptionsState.FinishedRefreshing)
            {
                UpdatingIndicator.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnNavigatedTo(System.Windows.Navigation.NavigationEventArgs e)
        {
            // Hook data contextes.
            DataContext = m_podcastsModel;
            this.EpisodeDownloadList.ItemsSource = m_episodeDownloadManager.EpisodeDownloadQueue;

            this.NowPlaying.SetupNowPlayingView();
            this.PlayHistory.ItemsSource = PodcastSqlModel.getInstance().getPlayHistory();
        }

        void PodcastPlayer_PodcastPlayerStarted(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/PodcastPlayerView.xaml", UriKind.Relative));
        }

        private void downloadListChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            int episodeDownloads = EpisodeDownloadList.Items.Count;
            switch (episodeDownloads)
            {
                case 0:
                    EpisodesDownloadingText.Visibility = Visibility.Visible;
                    break;
                case 1:
                    EpisodesDownloadingText.Visibility = Visibility.Collapsed;
                    break;
                default:
                    EpisodesDownloadingText.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void AddSubscriptionIconButton_Click(object sender, EventArgs e)
        {
            bool allowAddSubscription = true;
            if (App.IsTrial)
            {
                if (m_podcastsModel.PodcastSubscriptions.Count >= TRIAL_SUBSCRIPTION_LIMIT)
                {
                    allowAddSubscription = false;
                }
            }


            if (allowAddSubscription)
            {
                NavigationService.Navigate(new Uri("/Views/AddSubscription.xaml", UriKind.Relative));
            }
            else
            {
                MessageBox.Show("You have reached the limit of podcast subscriptions for this free trial of Podcatcher. Please purchase the full version from Windows Phone Marketplace.");
            }
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0) { 
                PodcastSubscriptionModel tappedSubscription = e.AddedItems[0] as PodcastSubscriptionModel;
                Debug.WriteLine("Showing episodes for podcast. Name: " + tappedSubscription.PodcastName);
                NavigationService.Navigate(new Uri(string.Format("/Views/PodcastEpisodes.xaml?podcastId={0}", tappedSubscription.PodcastId), UriKind.Relative));
                this.SubscriptionsList.SelectedIndex = -1;  // Aaargh... stupid Silverlight.
                tappedSubscription.NewEpisodesCount = "0";
            }
        }

        private void NavigationPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.ApplicationBar.IsVisible = this.NavigationPivot.SelectedIndex == 0 ? true : false;

            if (this.NavigationPivot.SelectedIndex == 2)
            {
                this.NowPlaying.SetupNowPlayingView();
                this.PlayHistory.ItemsSource = PodcastSqlModel.getInstance().getPlayHistory();
            }
        }

        private void AboutSubscriptionIconButton_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/AboutView.xaml", UriKind.Relative));
        }

        private void SettingsIconButton_Click(object sender, EventArgs e)
        {
            NavigationService.Navigate(new Uri("/Views/SettingsView.xaml", UriKind.Relative));
        }

/*        private List<PodcastEpisodeModel> playHistoryEpisodes()
        {
            List<PodcastEpisodeModel> history = new List<PodcastEpisodeModel>();

            if (m_applicationSettings.Contains(App.LSKEY_PODCAST_PLAY_HISTORY) == false)
            {
                m_applicationSettings.Add(App.LSKEY_PODCAST_PLAY_HISTORY, "");
                return history;
            }

            List<string> historyIds = (m_applicationSettings[App.LSKEY_PODCAST_PLAY_HISTORY] as string).Split(',').ToList();

            foreach (string id in historyIds)
            {
                if (String.IsNullOrEmpty(id))
                {
                    continue;
                }

                PodcastEpisodeModel episode = PodcastSqlModel.getInstance().episodeForEpisodeId(Int16.Parse(id));
                if (episode != null)
                {
                    history.Add(episode);
                }
            }

            return history;
        } */
    }
}
