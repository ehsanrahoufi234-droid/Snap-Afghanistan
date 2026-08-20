using System.Windows;
using SnapAfghanistan.Native.Services;
namespace SnapAfghanistan.Native.Dialogs
{
    public partial class PaymentHistoryDialog : Window
    {
        public PaymentHistoryDialog(SnapRepository repository, string centerId, string centerName) { InitializeComponent(); CenterName.Text = centerName; Grid.ItemsSource = repository.GetPayments(centerId); }
    }
}
