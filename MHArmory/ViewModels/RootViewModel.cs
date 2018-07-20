﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MHArmory.Core;
using MHArmory.Core.DataStructures;
using MHArmory.Search;
using MHArmory.Searching;

namespace MHArmory.ViewModels
{
    public class RootViewModel : ViewModelBase
    {
        public ICommand OpenSkillSelectorCommand { get; }
        public ICommand SearchArmorSetsCommand { get; }

        private SolverData solverData;
        private Solver solver;

        private bool isDataLoading = true;
        public bool IsDataLoading
        {
            get { return isDataLoading; }
            set { SetValue(ref isDataLoading, value); }
        }

        private bool isDataLoaded;
        public bool IsDataLoaded
        {
            get { return isDataLoaded; }
            set { SetValue(ref isDataLoaded, value); }
        }

        private IEnumerable<AbilityViewModel> selectedAbilities;
        public IEnumerable<AbilityViewModel> SelectedAbilities
        {
            get { return selectedAbilities; }
            set { SetValue(ref selectedAbilities, value); }
        }

        private IEnumerable<ArmorSetViewModel> foundArmorSets;
        public IEnumerable<ArmorSetViewModel> FoundArmorSets
        {
            get { return foundArmorSets; }
            set { SetValue(ref foundArmorSets, value); }
        }

        public InParametersViewModel InParameters { get; }

        private bool isSearching;
        public bool IsSearching
        {
            get { return isSearching; }
            private set { SetValue(ref isSearching, value); }
        }

        private bool isAutoSearch;
        public bool IsAutoSearch
        {
            get { return isAutoSearch; }
            set { SetValue(ref isAutoSearch, value); }
        }

        public RootViewModel()
        {
            OpenSkillSelectorCommand = new AnonymousCommand(OpenSkillSelector);
            SearchArmorSetsCommand = new AnonymousCommand(SearchArmorSets);

            InParameters = new InParametersViewModel();
        }

        private void OpenSkillSelector(object parameter)
        {
            RoutedCommands.OpenSkillsSelector.Execute(null);
        }

        public async void SearchArmorSets()
        {
            try
            {
                await SearchArmorSetsInternal();
            }
            catch (TaskCanceledException)
            {
            }
        }

        private CancellationTokenSource searchCancellationTokenSource;
        private Task previousSearchTask;

        private async Task SearchArmorSetsInternal()
        {
            if (searchCancellationTokenSource != null)
            {
                if (searchCancellationTokenSource.IsCancellationRequested)
                    return;

                searchCancellationTokenSource.Cancel();

                if (previousSearchTask != null)
                {
                    try
                    {
                        await previousSearchTask;
                    }
                    catch
                    {
                    }
                }
            }

            searchCancellationTokenSource = new CancellationTokenSource();
            previousSearchTask = Task.Run(() => SearchArmorSetsInternal(searchCancellationTokenSource.Token));

            IsSearching = true;

            try
            {
                await previousSearchTask;
            }
            finally
            {
                IsSearching = false;

                previousSearchTask = null;
                searchCancellationTokenSource = null;
            }
        }

        private async Task SearchArmorSetsInternal(CancellationToken cancellationToken)
        {
            if (SelectedAbilities == null)
                return;

            var desiredAbilities = SelectedAbilities
                .Where(x => x.IsChecked)
                .Select(x => x.Ability)
                .ToList();

            solverData = new SolverData(
                InParameters.Slots,
                null,
                GlobalData.Instance.Heads,
                GlobalData.Instance.Chests,
                GlobalData.Instance.Gloves,
                GlobalData.Instance.Waists,
                GlobalData.Instance.Legs,
                GlobalData.Instance.Charms,
                GlobalData.Instance.Jewels.Select(x => new SolverDataJewelModel(x)).ToList(),
                desiredAbilities
            );

            solverData.Done();

            solver = new Solver(solverData);

            solver.DebugData += Solver_DebugData;

            IList<ArmorSetSearchResult> result = await solver.SearchArmorSets(cancellationToken);

            if (result == null)
                FoundArmorSets = null;
            else
            {
                FoundArmorSets = result.Where(x => x.IsMatch).Select(x => new ArmorSetViewModel
                {
                    ArmorPieces = x.ArmorPieces,
                    Charm = x.Charm,
                    Jewels = x.Jewels.Select(j => new ArmorSetJewelViewModel { Jewel = j.Jewel, Count = j.Count }).ToList()
                });
            }

            //if (IsSearching)
            //    return;

            //IsSearching = true;

            //try
            //{
            //await SearchArmorSetsInternal();
            //}
            //finally
            //{
            //    IsSearching = false;
            //}

            //solverData.

            solver.DebugData -= Solver_DebugData;
        }

        internal void SelectedAbilitiesChanged()
        {
            GlobalData.Instance.Configuration.SelectedAbilities = SelectedAbilities
                .Where(x => x.IsChecked)
                .Select(x => x.Id)
                .ToArray();

            GlobalData.Instance.Configuration.Save();
        }

        private void Solver_DebugData(string debugData)
        {
            SearchResult = debugData;
        }

        private MaximizedSearchCriteria[] sortCriterias = new MaximizedSearchCriteria[]
        {
            MaximizedSearchCriteria.BaseDefense,
            MaximizedSearchCriteria.DragonResistance,
            MaximizedSearchCriteria.SlotSizeCube
        };

        private string searchResult;
        public string SearchResult
        {
            get { return searchResult; }
            private set { SetValue(ref searchResult, value); }
        }
    }
}
