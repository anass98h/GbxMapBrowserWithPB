using System.Collections.Generic;
using System.Windows;

namespace GbxMapBrowser
{
    public partial class OrganizeMapsByMedalWindow : Window
    {
        public IReadOnlyCollection<string> SelectedMedals { get; private set; } = [];

        public OrganizeMapsByMedalWindow()
        {
            InitializeComponent();
        }

        private void MoveMapsButton_Click(object sender, RoutedEventArgs e)
        {
            var medals = new List<string>();

            if (bronzeCheckBox.IsChecked == true)
            {
                medals.Add("Bronze");
            }

            if (silverCheckBox.IsChecked == true)
            {
                medals.Add("Silver");
            }

            if (goldCheckBox.IsChecked == true)
            {
                medals.Add("Gold");
            }

            if (authorCheckBox.IsChecked == true)
            {
                medals.Add("Author");
            }

            if (medals.Count == 0)
            {
                MessageBox.Show(
                    "Choose at least one medal.",
                    "No medals selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return;
            }

            SelectedMedals = medals;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
