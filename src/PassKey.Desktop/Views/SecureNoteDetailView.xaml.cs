using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.ApplicationModel.Resources;
using PassKey.Core.Constants;
using PassKey.Desktop.ViewModels;

namespace PassKey.Desktop.Views;

/// <summary>
/// Secure note editor view (right panel in master-detail layout).
/// Layout: header orizzontale (Titolo + unsaved dot | Categoria + dot), toggle Modifica/Anteprima,
/// area testo con ContentBox (edit) o MarkdownTextBlock (preview).
/// Footer: Elimina | Pin toggle | Salva.
/// </summary>
public sealed partial class SecureNoteDetailView : UserControl
{
    private SecureNoteDetailViewModel? _viewModel;
    private bool _updatingFromVm;
    private bool _isPreviewMode;

    public SecureNoteDetailView()
    {
        InitializeComponent();
        InitializeCategoryCombo();
    }

    public void SetViewModel(SecureNoteDetailViewModel vm)
    {
        _viewModel = vm;
        DataContext = vm;

        vm.PropertyChanged += OnViewModelPropertyChanged;

        // Popola UI dal ViewModel
        _updatingFromVm = true;
        TitleBox.Text = vm.Title;
        ContentBox.Text = vm.Content;
        CategoryCombo.SelectedIndex = (int)vm.Category;
        _updatingFromVm = false;

        // Accessibility: LabeledBy per form fields
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLabeledBy(TitleBox, TitleLabel);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLabeledBy(CategoryCombo, CategoryLabel);

        // Accessibility: announce word count on leaving content box
        ContentBox.LostFocus += ContentBox_LostFocus;

        // Contatori
        UpdateCounterText();

        // Dot colorato categoria
        UpdateCategoryDot(vm.Category);

        // Pin visual
        UpdatePinVisual();

        // Unsaved indicator
        UnsavedDot.Visibility = vm.HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;

        // Pulsante salva e visibilita elimina
        SaveButton.IsEnabled = vm.CanSave;
        DeleteButton.Visibility = vm.IsEditMode ? Visibility.Visible : Visibility.Collapsed;

        // Inizializza in modalita Modifica
        _isPreviewMode = false;
        UpdateViewMode();

        // Focus: titolo per nuove note, contenuto per note esistenti
        if (vm.IsEditMode)
            ContentBox.Focus(FocusState.Programmatic);
        else
            TitleBox.Focus(FocusState.Programmatic);
    }

    private void InitializeCategoryCombo()
    {
        foreach (NoteCategory cat in Enum.GetValues<NoteCategory>())
        {
            var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            var dot = new Border
            {
                Width = 12,
                Height = 12,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(ParseColor(SecureNotesListViewModel.GetCategoryColor(cat))),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = SecureNotesListViewModel.GetCategoryName(cat),
                VerticalAlignment = VerticalAlignment.Center
            };

            itemPanel.Children.Add(dot);
            itemPanel.Children.Add(label);

            var comboItem = new ComboBoxItem { Content = itemPanel, Tag = cat };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                comboItem, SecureNotesListViewModel.GetCategoryName(cat));
            CategoryCombo.Items.Add(comboItem);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SecureNoteDetailViewModel.CanSave):
                SaveButton.IsEnabled = _viewModel?.CanSave ?? false;
                break;
            case nameof(SecureNoteDetailViewModel.IsSaving):
                UpdateSavingState(_viewModel?.IsSaving ?? false);
                break;
            case nameof(SecureNoteDetailViewModel.CharacterCount):
            case nameof(SecureNoteDetailViewModel.WordCount):
                UpdateCounterText();
                break;
            case nameof(SecureNoteDetailViewModel.IsEditMode):
                DeleteButton.Visibility = (_viewModel?.IsEditMode ?? false)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                break;
            case nameof(SecureNoteDetailViewModel.HasUnsavedChanges):
                var hasChanges = _viewModel?.HasUnsavedChanges ?? false;
                UnsavedDot.Visibility = hasChanges ? Visibility.Visible : Visibility.Collapsed;
                if (hasChanges)
                    Announce("Modifiche non salvate");
                break;
            case nameof(SecureNoteDetailViewModel.IsPinned):
                UpdatePinVisual();
                break;
        }
    }

    // --- Counter ---

    private void UpdateCounterText()
    {
        CharCountText.Text = $"{_viewModel?.CharacterCount ?? 0} car · {_viewModel?.WordCount ?? 0} parole";
    }

    // --- Toggle Modifica / Anteprima ---

    private void EditModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _isPreviewMode = false;
        UpdateViewMode();
    }

    private void PreviewModeBtn_Click(object sender, RoutedEventArgs e)
    {
        _isPreviewMode = true;
        UpdateViewMode();
    }

    private void UpdateViewMode()
    {
        if (_isPreviewMode)
        {
            MarkdownPreview.Text = _viewModel?.Content ?? string.Empty;
            ContentBox.Visibility = Visibility.Collapsed;
            PreviewScroll.Visibility = Visibility.Visible;
            EditModeBtn.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
            PreviewModeBtn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            PreviewScroll.Focus(FocusState.Programmatic);
        }
        else
        {
            ContentBox.Visibility = Visibility.Visible;
            PreviewScroll.Visibility = Visibility.Collapsed;
            EditModeBtn.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            PreviewModeBtn.Style = (Style)Application.Current.Resources["SubtleButtonStyle"];
            ContentBox.Focus(FocusState.Programmatic);
        }
    }

    // --- Categoria dot colorato ---

    private void UpdateCategoryDot(NoteCategory category)
    {
        CategoryDot.Fill = new SolidColorBrush(
            ParseColor(SecureNotesListViewModel.GetCategoryColor(category)));
    }

    // --- Pin toggle (istantaneo, senza passare da Salva) ---

    private void PinToggle_Click(object sender, RoutedEventArgs e)
    {
        _viewModel?.TogglePinCommand.Execute(null);
        // Il visual si aggiorna via OnIsPinnedChanged → PropertyChanged
    }

    private void UpdatePinVisual()
    {
        var pinned = _viewModel?.IsPinned ?? false;
        PinToggle.IsChecked = pinned;
        var loader = new ResourceLoader();
        PinToggleText.Text = pinned ? loader.GetString("NotesPinnedButton") : loader.GetString("NotesPinButtonLabel");

        // Accessibility: nome dinamico descrive l'azione futura (toggle pattern)
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            PinToggle, pinned ? "Rimuovi dalla cima" : "Fissa nota in cima alla lista");
    }

    // --- TextBox → ViewModel sync ---

    private void TitleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Title = TitleBox.Text;
    }

    private void ContentBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null)
            _viewModel.Content = ContentBox.Text;
    }

    private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_updatingFromVm && _viewModel is not null &&
            CategoryCombo.SelectedItem is ComboBoxItem item && item.Tag is NoteCategory cat)
        {
            _viewModel.Category = cat;
            UpdateCategoryDot(cat);
        }
    }

    // --- Pulsanti footer ---

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.SaveCommand.ExecuteAsync(null);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            await _viewModel.DeleteCommand.ExecuteAsync(null);
    }

    private void UpdateSavingState(bool saving)
    {
        SaveProgress.IsActive = saving;
        SaveProgress.Visibility = saving ? Visibility.Visible : Visibility.Collapsed;
        var saveLoader = new ResourceLoader();
        SaveButtonText.Text = saving ? saveLoader.GetString("SaveInProgress") : saveLoader.GetString("ButtonSave");
        SaveButton.IsEnabled = !saving;
        Announce(saving ? "Salvataggio in corso..." : "Salva completato");
    }

    // --- Helpers ---

    private void ContentBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            Announce($"{_viewModel.CharacterCount} caratteri, {_viewModel.WordCount} parole");
    }

    private void Announce(string message)
    {
        A11yAnnouncer.Text = "";
        A11yAnnouncer.Text = message;
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        return ColorHelper.FromArgb(255,
            byte.Parse(hex[..2], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[2..4], System.Globalization.NumberStyles.HexNumber),
            byte.Parse(hex[4..6], System.Globalization.NumberStyles.HexNumber));
    }
}
