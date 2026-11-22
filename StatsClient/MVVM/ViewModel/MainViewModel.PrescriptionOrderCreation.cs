using StatsClient.MVVM.Core;
using StatsClient.MVVM.Model;
using StatsClient.MVVM.View;
using StatsClient.MVVM.ViewModel;
using System.Windows;

namespace StatsClient.MVVM.ViewModel;

public partial class MainViewModel
{
    private OrderCreationWindow? _orderCreationWindow;
    private bool initiateOrderCreation;

    public RelayCommand ShowOrderCreationWindowCommand { get; private set; } = null!;

    public bool InitiateOrderCreation
    {
        get => initiateOrderCreation;
        set
        {
            if (initiateOrderCreation == value)
            {
                return;
            }

            initiateOrderCreation = value;
            RaisePropertyChanged(nameof(InitiateOrderCreation));
        }
    }

    private void WireShowOrderCreationWindowCommand()
    {
        ShowOrderCreationWindowCommand = new RelayCommand(_ => OpenOrderCreationWindow());
    }

    private void OpenOrderCreationWindow()
    {
        if (LastPrescriptionPdfExtractedFields is null)
        {
            return;
        }

        string panNumber = !string.IsNullOrWhiteSpace(LastUsedPanNumber)
            ? LastUsedPanNumber
            : NextPanNumberGlobal;

        if (string.IsNullOrWhiteSpace(panNumber))
        {
            return;
        }

        ShowOrderCreationWindow(LastPrescriptionPdfExtractedFields, panNumber);
    }

    private void ShowOrderCreationWindow(PrescriptionPdfExtractedFields fields, string panNumber)
    {
        if (_MainWindow is null)
        {
            return;
        }

        if (_orderCreationWindow is { IsVisible: true })
        {
            _orderCreationWindow.Activate();
            return;
        }

        if (_orderCreationWindow is not null)
        {
            try
            {
                _orderCreationWindow.Close();
            }
            catch
            {
            }

            _orderCreationWindow = null;
        }

        var viewModel = new OrderCreationViewModel();
        viewModel.LoadFromExtractedFields(fields, panNumber);

        _orderCreationWindow = new OrderCreationWindow(_MainWindow, viewModel);
        _orderCreationWindow.Closed += (_, _) => _orderCreationWindow = null;
        _orderCreationWindow.Show();
        _orderCreationWindow.Activate();
    }

    internal void CloseOrderCreationWindowForShutdown()
    {
        if (_orderCreationWindow is null)
        {
            return;
        }

        try
        {
            _orderCreationWindow.Close();
        }
        catch
        {
        }

        _orderCreationWindow = null;
    }
}
