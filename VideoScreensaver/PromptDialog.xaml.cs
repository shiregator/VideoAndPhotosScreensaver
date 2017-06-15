using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace VideoScreensaver
{
    /// <summary>
    /// Логика взаимодействия для PromptDialog.xaml
    /// </summary>
    public partial class PromptDialog : Window
    {
        private String _acceptedAnswers;

        public String Input
        {
            get { return UserInput.Text; }
            set { UserInput.Text = value; }
        }

        public PromptDialog(String title, String prompt, String AcceptedAnswers="yes,ok", String CancellText="Cancel")
        {
            InitializeComponent();
            _acceptedAnswers = AcceptedAnswers;
            Title = title;
            CancelButtonText.Text = CancellText;
            PromptBlock.Text = prompt;
            CancelButton.Click += (obj, e) =>
            {
                DialogResult = false;
                Close();
            };
            UserInput.TextChanged += (obj, e) =>
            {
                if (AcceptedAnswers.ToLower().Split(',').Contains(UserInput.Text.ToLower()))
                {
                    DialogResult = true;
                    Close();
                }
            };
        }
    }
}
