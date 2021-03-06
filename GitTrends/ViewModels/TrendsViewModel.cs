﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AsyncAwaitBestPractices.MVVM;
using GitTrends.Shared;

namespace GitTrends
{
    class TrendsViewModel : BaseViewModel
    {
        readonly GitHubApiV3Service _gitHubApiV3Service;

        bool _isFetchingData = true;
        List<DailyViewsModel> _dailyViewsList = new List<DailyViewsModel>();
        List<DailyClonesModel> _dailyClonesList = new List<DailyClonesModel>();

        public TrendsViewModel(GitHubApiV3Service gitHubApiV3Service, AnalyticsService analyticsService) : base(analyticsService)
        {
            _gitHubApiV3Service = gitHubApiV3Service;
            FetchDataCommand = new AsyncCommand<(string Owner, string Repository)>(repo => ExecuteFetchDataCommand(repo.Owner, repo.Repository));
        }

        public ICommand FetchDataCommand { get; }
        public double DailyViewsClonesMinValue { get; } = 0;

        public bool IsChartVisible => !IsFetchingData;
        public DateTime MinDateValue => GetMinimumLocalDateTime();
        public DateTime MaxDateValue => GetMaximumLocalDateTime();

        public double DailyViewsClonesMaxValue
        {
            get
            {
                var dailyViewMaxValue = DailyViewsList.Any() ? DailyViewsList.Max(x => x.TotalViews) : 0;
                var dailyClonesMaxValue = DailyClonesList.Any() ? DailyClonesList.Max(x => x.TotalClones) : 0;

                return Math.Max(dailyViewMaxValue, dailyClonesMaxValue);
            }
        }

        public bool IsFetchingData
        {
            get => _isFetchingData;
            set => SetProperty(ref _isFetchingData, value, () => OnPropertyChanged(nameof(IsChartVisible)));
        }

        public List<DailyViewsModel> DailyViewsList
        {
            get => _dailyViewsList;
            set => SetProperty(ref _dailyViewsList, value, UpdateDailyViewsListPropertiesChanged);
        }

        public List<DailyClonesModel> DailyClonesList
        {
            get => _dailyClonesList;
            set => SetProperty(ref _dailyClonesList, value, UpdateDailyClonesListPropertiesChanged);
        }

        static DateTimeOffset GetMinimumDateTimeOffset(in IEnumerable<DailyViewsModel> dailyViewsList, in IEnumerable<DailyClonesModel> dailyClonesList)
        {
            var minViewsDateTimeOffset = dailyViewsList.Any() ? dailyViewsList.Min(x => x.Day) : DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(13));
            var minClonesDateTimeOffset = dailyClonesList.Any() ? dailyClonesList.Min(x => x.Day) : DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(13));

            return new DateTime(Math.Min(minViewsDateTimeOffset.Ticks, minClonesDateTimeOffset.Ticks));
        }

        static DateTimeOffset GetMaximumDateTimeOffset(in IEnumerable<DailyViewsModel> dailyViewsList, in IEnumerable<DailyClonesModel> dailyClonesList)
        {
            var maxViewsDateTime = dailyViewsList.Any() ? dailyViewsList.Max(x => x.Day) : DateTimeOffset.UtcNow;
            var maxClonesDateTime = dailyClonesList.Any() ? dailyClonesList.Max(x => x.Day) : DateTimeOffset.UtcNow;

            return new DateTime(Math.Max(maxViewsDateTime.Ticks, maxClonesDateTime.Ticks));
        }

        static DateTime GetMinimumLocalDateTime(in IEnumerable<DailyViewsModel> dailyViewsList, in IEnumerable<DailyClonesModel> dailyClonesList)
        {
            var minViewsDateTimeOffset = dailyViewsList.Any() ? dailyViewsList.Min(x => x.LocalDay) : DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(13));
            var minClonesDateTimeOffset = dailyClonesList.Any() ? dailyClonesList.Min(x => x.LocalDay) : DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(13));

            return new DateTime(Math.Min(minViewsDateTimeOffset.Ticks, minClonesDateTimeOffset.Ticks));
        }

        static DateTime GetMaximumLocalDateTime(in IEnumerable<DailyViewsModel> dailyViewsList, in IEnumerable<DailyClonesModel> dailyClonesList)
        {
            var maxViewsDateTime = dailyViewsList.Any() ? dailyViewsList.Max(x => x.LocalDay) : DateTimeOffset.UtcNow;
            var maxClonesDateTime = dailyClonesList.Any() ? dailyClonesList.Max(x => x.LocalDay) : DateTimeOffset.UtcNow;

            return new DateTime(Math.Max(maxViewsDateTime.Ticks, maxClonesDateTime.Ticks));
        }

        async Task ExecuteFetchDataCommand(string owner, string repository)
        {
            IsFetchingData = true;

            try
            {
                var getRepositoryViewStatisticsTask = _gitHubApiV3Service.GetRepositoryViewStatistics(owner, repository);
                var getRepositoryCloneStatisticsTask = _gitHubApiV3Service.GetRepositoryCloneStatistics(owner, repository);
                var minimumTimeTask = Task.Delay(2000);

                await Task.WhenAll(getRepositoryViewStatisticsTask, getRepositoryCloneStatisticsTask, minimumTimeTask).ConfigureAwait(false);

                var repositoryViews = await getRepositoryViewStatisticsTask.ConfigureAwait(false);
                var repositoryClones = await getRepositoryCloneStatisticsTask.ConfigureAwait(false);

                addMissingDates(repositoryViews.DailyViewsList, repositoryClones.DailyClonesList);

                DailyViewsList = repositoryViews.DailyViewsList.OrderBy(x => x.Day).ToList();
                DailyClonesList = repositoryClones.DailyClonesList.OrderBy(x => x.Day).ToList();

                PrintDays();
            }
            finally
            {
                IsFetchingData = false;
            }

            static void addMissingDates(in IList<DailyViewsModel> dailyViewsList, in IList<DailyClonesModel> dailyClonesList)
            {
                var day = GetMinimumDateTimeOffset(dailyViewsList, dailyClonesList);
                var maximumDay = GetMaximumDateTimeOffset(dailyViewsList, dailyClonesList);

                var viewsDays = dailyViewsList.Select(x => x.Day.Day).ToList();
                var clonesDays = dailyClonesList.Select(x => x.Day.Day).ToList();

                while (day.Day != maximumDay.AddDays(1).Day)
                {
                    if (!viewsDays.Contains(day.Day))
                        dailyViewsList.Add(new DailyViewsModel(removeHourMinuteSecond(day), 0, 0));

                    if (!clonesDays.Contains(day.Day))
                        dailyClonesList.Add(new DailyClonesModel(removeHourMinuteSecond(day), 0, 0));

                    day = day.AddDays(1);
                }
            }

            static DateTimeOffset removeHourMinuteSecond(in DateTimeOffset date) => new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        }

        void UpdateDailyClonesListPropertiesChanged()
        {
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(DailyViewsClonesMinValue));
            OnPropertyChanged(nameof(MinDateValue));
            OnPropertyChanged(nameof(MaxDateValue));
        }

        void UpdateDailyViewsListPropertiesChanged()
        {
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(DailyViewsClonesMaxValue));
            OnPropertyChanged(nameof(MinDateValue));
            OnPropertyChanged(nameof(MaxDateValue));
        }

        DateTimeOffset GetMinimumDateTimeOffset() => GetMinimumDateTimeOffset(DailyViewsList, DailyClonesList);
        DateTimeOffset GetMaximumDateTimeOffset() => GetMaximumDateTimeOffset(DailyViewsList, DailyClonesList);

        DateTime GetMinimumLocalDateTime() => GetMinimumLocalDateTime(DailyViewsList, DailyClonesList);
        DateTime GetMaximumLocalDateTime() => GetMaximumLocalDateTime(DailyViewsList, DailyClonesList);

        [Conditional("DEBUG")]
        void PrintDays()
        {
            Debug.WriteLine("Clones");
            foreach (var cloneDay in DailyClonesList.Select(x => x.Day))
                Debug.WriteLine(cloneDay);

            Debug.WriteLine("");

            Debug.WriteLine("Views");
            foreach (var viewDay in DailyViewsList.Select(x => x.Day))
                Debug.WriteLine(viewDay);
        }
    }
}
