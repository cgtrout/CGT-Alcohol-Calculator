﻿using BloodAlcoholCalculator.Repository;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BloodAlcoholCalculator.CalculationUnit;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace BloodAlcoholCalculator.ViewModel
{
     public class MainDrinkViewModel : WorkspaceViewModel, IDropTarget
     {
          public ConsumedDrinkListViewModel ConsumedDrinkListVM { get; set; }
          public DataRepository<UserViewModel, Model.User> UserRepository { get; set; }
          public DataRepository<ConsumedDrinkViewModel, Model.ConsumedDrink> ConsumedDrinkRepository { get; set; }
          private UserViewModel _user = null;
          private DateTime _startTime;
          private DispatcherTimer timer;
          private RelayCommand _selectNowCommand;

          private Stopwatch stopWatch;
          private TimeSpan chartRefreshCountDown;
          private TimeSpan chartRefreshCountDownStart = new TimeSpan(0, 1, 0);
          private RelayCommand _refreshChartCommand;

          public MainDrinkViewModel()
          {
               base.DisplayName = "Main Drink Window";
               ConsumedDrinkListVM = new ConsumedDrinkListViewModel();
               ConsumedDrinkRepository = DataRepository<ConsumedDrinkViewModel, Model.ConsumedDrink>.Instance;
               UserRepository = DataRepository<UserViewModel, Model.User>.Instance;

               timer = new DispatcherTimer();
               timer.Interval = new TimeSpan(0, 0, 1);
               timer.Tick += Timer_Tick;
               stopWatch = new Stopwatch();
               stopWatch.Start();
               chartRefreshCountDown = chartRefreshCountDownStart;
          }

          private void Timer_Tick(object sender, EventArgs e)
          {
               OnPropertyChanged("BloodAlcohol");
               OnPropertyChanged("BloodAlcoholString");

               chartRefreshCountDown -= stopWatch.Elapsed;
               stopWatch.Restart();
                    
               if(chartRefreshCountDown.Ticks <= 0) {
                    OnPropertyChanged("PlotPoints");
                    chartRefreshCountDown = chartRefreshCountDownStart;
               }
               
          }

          public UserViewModel User
          {
               get {
                    return _user;
               }
               set {
                    _user = value;
                    OnPropertyChanged("User");
                    StartTime = value.StartTime;
                    timer.Start();
                    ConsumedDrinkListVM.LinkDrinks();
                    ConsumedDrinkListVM.User = value;
               }
          }

          public DateTime StartTime {
               get {
                    return _startTime;
               }
               set {
                    _startTime = value;
                    User.StartTime = value;
                    UserRepository.Edit(User);
                    OnPropertyChanged("StartTime");
               }
          }

          public double BloodAlcohol
          {
               get {
                    if (User == null) return 0;

                    var deltaTime = DateTime.Now - User.StartTime;
                    return CalculationUnit.BacCalculationUnit.CalculateBac(ConsumedDrinkListVM.GetFilteredList(), User.BaseUser, deltaTime);
               }
          }

          public string BloodAlcoholString => $"BAC={BloodAlcohol.ToString("0.000")}";

          //SelectNowCommand
          public ICommand SelectNowCommand
          {
               get {
                    if (_selectNowCommand == null) {
                         _selectNowCommand = new RelayCommand(SelectNow);
                    }
                    return _selectNowCommand;
               }
          }

          private void SelectNow(object obj)
          {
               if (User == null) return;
               StartTime = DateTime.Now;
          }

          //
          //Drag Drop
          //
          public void DragOver(IDropInfo dropInfo)
          {
               var sourceItem = dropInfo.Data as DefinedDrinkViewModel;
               var targetItem = dropInfo.TargetItem as MainDrinkViewModel;

               if (sourceItem != null) {
                    dropInfo.Effects = DragDropEffects.Copy;
               }
          }

          public void Drop(IDropInfo dropInfo)
          {
               var definedDrink = dropInfo.Data as DefinedDrinkViewModel;

               if (User != null && definedDrink != null) {
                    var consumedDrink = new ConsumedDrinkViewModel() {
                         LinkedDrink = definedDrink.Drink,
                         Time = DateTime.Now
                    };
                    consumedDrink.Drink.LinkedUserId = User.Id;
                    consumedDrink.Drink.LinkedDrinkId = definedDrink.Id;
                    ConsumedDrinkRepository.Add(consumedDrink);
               }
          }

          //
          //Charting
          //
          private List<DateTimePoint> CreatePoints()
          {
               var drinkList = ConsumedDrinkListVM.GetFilteredList();
               var points = new List<DateTimePoint>();

               if (User == null || !drinkList.Any()) return points;

               //create point for every minute
               DateTime start = drinkList[0].Time;
               DateTime currTime = start;

               while (true) {
                    var thisDrinkList = new List<Model.ConsumedDrink>();

                    //get list of drinks up to this point
                    foreach (var d in drinkList) {
                         if (d.Time > currTime) {
                              break;
                         }
                         thisDrinkList.Add(d);
                    }

                    var deltaTime = currTime - StartTime;

                    //calc bac
                    var bac = BacCalculationUnit.CalculateBac(thisDrinkList, User.BaseUser, deltaTime);

                    points.Add(new DateTimePoint() { X = currTime, Y = bac });
                    currTime = currTime.AddMinutes(1.0);

                    //keep plotting until bac falls to zero
                    if (bac < 0) {
                         break;
                    }
               }

               return points;
          }

          public ObservableCollection<DateTimePoint> PlotPoints
          {
               get
               {
                    return new ObservableCollection<DateTimePoint>(CreatePoints());
               }
          }

          //RefreshChartCommand
          public ICommand RefreshChartCommand
          {
               get
               {
                    if (_refreshChartCommand == null) {
                         _refreshChartCommand = new RelayCommand(x => {
                              OnPropertyChanged("PlotPoints");
                         });
                    }
                    return _refreshChartCommand;
               }
          }
     }

     public class DateTimePoint
     {
          public DateTime X { get; set; }
          public double Y { get; set; }
     }
}
