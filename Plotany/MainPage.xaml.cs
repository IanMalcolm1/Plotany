﻿namespace Plotany
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            this.BindingContext = new MapViewModel();
        }
    }

}
