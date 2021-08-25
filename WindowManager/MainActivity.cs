﻿using Android.App;
using Android.Graphics;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.Util;
using AndroidX.Window;
using Java.Lang;
using Java.Util.Concurrent;

/*
 This sample is a C# port of this Kotlin code
 https://github.com/googlecodelabs/android-foldable-codelab/tree/main/window-manager
 which is part of a Google Codelab that explains how to use Window Manager

19-Jul-21 Update to androidx.window-1.0.0-apha09
		  FoldingFeature API changes - some properties became methods (GetOrientation, GetState, GetOcclusionType) and their types became "enums" (static class fields)
          Use OnStart/Stop instead of OnAttachedToWindow/OnDetached
 */
namespace WindowManagerDemo
{
    [Activity(Label = "@string/app_name",
    Theme = "@style/AppTheme",
    MainLauncher = true)]//, // HACK: for some reason the Window Manager doesn't work when configuration changes are being handled 
    //ConfigurationChanges = Android.Content.PM.ConfigChanges.ScreenSize | Android.Content.PM.ConfigChanges.ScreenLayout | Android.Content.PM.ConfigChanges.Orientation | Android.Content.PM.ConfigChanges.SmallestScreenSize)]
    public class MainActivity : AppCompatActivity, IConsumer
    {
        const string TAG = "JWM"; // Jetpack Window Manager
        WindowManager wm;

        ConstraintLayout constraintLayout;
        TextView windowMetrics, layoutChange, configurationChanged;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            wm = new WindowManager(this);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            constraintLayout = FindViewById<ConstraintLayout>(Resource.Id.constraint_layout);
            windowMetrics = FindViewById<TextView>(Resource.Id.window_metrics);
            layoutChange = FindViewById<TextView>(Resource.Id.layout_change);
            configurationChanged = FindViewById<TextView>(Resource.Id.configuration_changed);
        }

        void printLayoutStateChange(WindowLayoutInfo newLayoutInfo)
        {
            Log.Info(TAG, wm.CurrentWindowMetrics.Bounds.ToString());
            Log.Info(TAG, wm.MaximumWindowMetrics.Bounds.ToString());
            windowMetrics.Text = $"CurrentWindowMetrics: {wm.CurrentWindowMetrics.Bounds}\n" +
                $"MaximumWindowMetrics: {wm.MaximumWindowMetrics.Bounds}";

            layoutChange.Text = newLayoutInfo.ToString();

            configurationChanged.Text = "One logic/physical display - unspanned";

            foreach (var displayFeature in newLayoutInfo.DisplayFeatures)
            {
                if (displayFeature is FoldingFeature foldingFeature)
                {
                    alignViewToDeviceFeatureBoundaries(newLayoutInfo);
                    
                    if (foldingFeature.GetOcclusionType() == FoldingFeature.OcclusionType.None)
                    {
                        configurationChanged.Text = "App is spanned across a fold";
                    }
                    if (foldingFeature.GetOcclusionType() == FoldingFeature.OcclusionType.Full)
                    {
                        configurationChanged.Text = "App is spanned across a hinge";
                    }
                    configurationChanged.Text += "\nIsSeparating: " + foldingFeature.IsSeparating
                            + "\nOrientation: " + foldingFeature.GetOrientation()  // FoldingFeature.Orientation.Vertical or Horizontal
                            + "\nState: " + foldingFeature.GetState(); // FoldingFeature.StateFlat or StateHalfOpened
                }
                else
                {
                    Log.Info(TAG, "DisplayFeature is not a fold or hinge");
                }
            }
        }

        void alignViewToDeviceFeatureBoundaries(WindowLayoutInfo newLayoutInfo)
        {
            var set = new ConstraintSet();
            set.Clone(constraintLayout); // existing constraints baseline
            var foldFeature = newLayoutInfo.DisplayFeatures[0] as FoldingFeature;
            //We get the display feature bounds.
            var rect = foldFeature.Bounds;
            //Set the red hinge indicator's width and height using the Bounds
            set.ConstrainHeight(Resource.Id.device_feature, rect.Bottom - rect.Top);
            set.ConstrainWidth(Resource.Id.device_feature, rect.Right - rect.Left);

            set.Connect(
                Resource.Id.device_feature, ConstraintSet.Start,
                ConstraintSet.ParentId, ConstraintSet.Start, 0
            );

            set.Connect(
                Resource.Id.device_feature, ConstraintSet.Top,
                ConstraintSet.ParentId, ConstraintSet.Top, 0
            );
            
            if (foldFeature.GetOrientation() == FoldingFeature.Orientation.Vertical)
            {
                // Device feature is placed vertically
                set.SetMargin(Resource.Id.device_feature, ConstraintSet.Start, rect.Left);
                set.Connect(
                    Resource.Id.layout_change, ConstraintSet.End,
                    Resource.Id.device_feature, ConstraintSet.Start, 0
                );
            }
            else
            {
                //Device feature is placed horizontally
                var statusBarHeight = calculateStatusBarHeight();
                var toolBarHeight = calculateToolbarHeight();

                set.SetMargin(
                    Resource.Id.device_feature, ConstraintSet.Top,
                    rect.Top - statusBarHeight - toolBarHeight
                );
                set.Connect(
                    Resource.Id.layout_change, ConstraintSet.Top,
                    Resource.Id.device_feature, ConstraintSet.Bottom, 0
                );
            }

            //Set the view to visible and apply constraints
            set.SetVisibility(Resource.Id.device_feature, (int)SystemUiFlags.Visible); // public static final int VISIBLE = 0x00000000;
            set.ApplyTo(constraintLayout);
        }

        int calculateToolbarHeight()
        {
            var typedValue = new TypedValue();
            if (Theme.ResolveAttribute(Android.Resource.Attribute.ActionBarSize, typedValue, true))
            {
                return TypedValue.ComplexToDimensionPixelSize(typedValue.Data, Resources.DisplayMetrics);
            }
            else
            {
                return 0;
            }
        }

        int calculateStatusBarHeight()
        {
            var rect = new Rect();
            Window.DecorView.GetWindowVisibleDisplayFrame(rect);
            return rect.Top;
        }

        IExecutor runOnUiThreadExecutor()
        {
            return new MyExecutor();
        }
        class MyExecutor : Java.Lang.Object, IExecutor
        {
            Handler handler = new Handler(Looper.MainLooper);
            public void Execute(IRunnable r)
            {
                handler.Post(r);
            }
        }

        public void Accept(Java.Lang.Object newLayoutInfo)  // Object will be WindowLayoutInfo
        {
            Log.Info(TAG, "===LayoutStateChangeCallback.Accept");
            Log.Info(TAG, newLayoutInfo.ToString());
            printLayoutStateChange(newLayoutInfo as WindowLayoutInfo);
        }

        protected override void OnStart()
        {
            base.OnStart();
            wm.RegisterLayoutChangeCallback(runOnUiThreadExecutor(), this);
        }

        protected override void OnStop()
        {
            base.OnStop();
            wm.UnregisterLayoutChangeCallback(this);
        }
    }
}