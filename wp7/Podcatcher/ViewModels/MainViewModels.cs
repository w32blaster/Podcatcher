﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Data.Linq;
using System.Diagnostics;

namespace Podcatcher.ViewModels
{
    public class MainViewModels : INotifyPropertyChanged
    {
        /************************************* Public properties *******************************/

        private ObservableCollection<PodcastSubscriptionModel> m_podcastSubscriptions = null;
        public ObservableCollection<PodcastSubscriptionModel> PodcastSubscriptions
        {
            get
            {
                if (m_podcastSubscriptions == null) {
                    using (var db = new PodcastSqlModel())
                    {
                        var query = from PodcastSubscriptionModel podcastSubscription in db.Subscriptions
                                    orderby podcastSubscription.PodcastName
                                    select podcastSubscription;

                        m_podcastSubscriptions = new ObservableCollection<PodcastSubscriptionModel>(query);
                    }
                }

                return m_podcastSubscriptions;
            }

            set
            {
                NotifyPropertyChanged("PodcastSubscriptions");
            }
        }

        private ObservableCollection<PodcastEpisodeModel> m_playHistory = new ObservableCollection<PodcastEpisodeModel>();
        public ObservableCollection<PodcastEpisodeModel> PlayHistoryListProperty
        {
            get
            {
                return new ObservableCollection<PodcastEpisodeModel>(createPlayHistory());
            }

            set 
            {
                NotifyPropertyChanged("PlayHistoryListProperty");
            }
        }

        private List<PodcastEpisodeModel> createPlayHistory()
        {
            List<PodcastEpisodeModel> playHistory = new List<PodcastEpisodeModel>();

            using (var db = new PodcastSqlModel())
            {
                var query = from LastPlayedEpisodeModel e in db.PlayHistory
                            orderby e.TimeStamp descending
                            select e;

                int itemsCount = 0;
                foreach (LastPlayedEpisodeModel e in query)
                {
                    PodcastEpisodeModel episode = db.episodeForEpisodeId(e.LastPlayedEpisodeId);
                    if (episode == null)
                    {
                        Debug.WriteLine("Got NULL episode for play history. This probably means the subscription has been deleted.");
                        continue;
                    }

                    playHistory.Add(episode);

                    itemsCount++;
                    if (itemsCount >= 4)
                    {
                        break;
                    }
                }

                return playHistory;
            }
        }

        #region propertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (null != handler)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        #endregion
    }

}