﻿using ClangFormatEditor.Enums;
using ClangFormatEditor.Helpers;
using ClangFormatEditor.Interfaces;
using ClangFormatEditor.MVVM.Controllers;
using ClangFormatEditor.MVVM.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace ClangFormatEditor
{
  public class ConfiguratorViewModel : InputProvider, INotifyPropertyChanged, IFormatEditor
  {
    #region Members

    public event PropertyChangedEventHandler PropertyChanged;

    private readonly ConfiguratorView configuratorView;
    private ICommand selctCodeFileCommand;
    private ICommand createFormatFileCommand;
    private ICommand importFormatFileCommand;
    private ICommand formatCodeCommand;
    private ICommand resetCommand;
    private ICommand openUri;
    private ICommand resetSearchCommand;

    private List<IFormatOption> searchResultFormatStyleOptions;
    private string input = AppConstants.InputCodeText;
    private string checkSearch = string.Empty;
    private bool showOptionDescription = true;
    private bool windowLoaded = false;
    private string inputLineNumber;
    private string outputLineNumber;
    private string nameColumnWidth;
    private string droppedFile;


    private const string nameColumnWidthMax = "340";


    #endregion

    #region Constructor

    public ConfiguratorViewModel(ConfiguratorView configuratorView)
    {
      configuratorView.Loaded += EditorLoaded;
      this.configuratorView = configuratorView;
      InitializeStyleOptions(FormatOptionsProvider.CustomOptionsData);
      SetOutputTextAsync(AppConstants.OutputCodeText).SafeFireAndForget();
    }

    #endregion

    #region Properties

    public List<IFormatOption> FormatOptions
    {
      get
      {
        if (string.IsNullOrWhiteSpace(checkSearch))
        {
          return formatStyleOptions;
        }
        return searchResultFormatStyleOptions;
      }
      set
      {
        formatStyleOptions = value;
        OnPropertyChanged(nameof(FormatOptions));
      }
    }

    public string CheckSearch
    {
      get => checkSearch;
      set
      {
        checkSearch = value;
        FindFormatOptionsAsync(checkSearch).SafeFireAndForget();
        OnPropertyChanged(nameof(CheckSearch));
      }
    }

    public IFormatOption SelectedOption
    {
      get => selectedOption;
      set
      {
        selectedOption = value;
        OnPropertyChanged(nameof(SelectedOption));
      }
    }

    public static IEnumerable<FormatStyle> Styles
    {
      get => Enum.GetValues(typeof(FormatStyle)).Cast<FormatStyle>();
    }

    public IEnumerable<ToggleValues> BooleanComboboxValues
    {
      get => Enum.GetValues(typeof(ToggleValues)).Cast<ToggleValues>();
    }

    public FormatStyle SelectedStyle
    {
      get
      {
        FindFormatOptionsAsync(checkSearch).SafeFireAndForget();
        return selectedStyle;
      }
      set
      {
        selectedStyle = value;
        ChangeControlsDependingOnStyle();
        OnPropertyChanged(nameof(SelectedStyle));

        RunFormat();
      }
    }

    public string NameColumnWidth
    {
      get => nameColumnWidth;
      set
      {
        nameColumnWidth = value;
        OnPropertyChanged(nameof(NameColumnWidth));
      }
    }

    public string EnableOptionColumnWidth
    {
      get => nameColumnWidth;
      set
      {
        nameColumnWidth = value;
        OnPropertyChanged(nameof(EnableOptionColumnWidth));
      }
    }

    public static bool CanExecute
    {
      get => true;
    }

    public bool ShowOptionDescription
    {
      get => showOptionDescription;
      set
      {
        showOptionDescription = value;
        OnPropertyChanged(nameof(ShowOptionDescription));
      }
    }

    public string Input
    {
      get
      {
        var lineCount = input.Split(Environment.NewLine).Length;
        SetInputLineNumberAsync(lineCount).SafeFireAndForget();
        return input;
      }
      set
      {
        input = value;
        if (IsAnyOptionEnabled())
        {
          OnPropertyChanged(nameof(Input));
          RunFormat();
        };
      }
    }

    public string InputLineNumber
    {
      get => inputLineNumber;
      set
      {
        inputLineNumber = value;
        OnPropertyChanged(nameof(InputLineNumber));
      }
    }

    public string OutputLineNumber
    {
      get => outputLineNumber;
      set
      {
        outputLineNumber = value;
        OnPropertyChanged(nameof(OutputLineNumber));
      }
    }

    #endregion


    #region Commands

    public ICommand CreateFormatFileCommand
    {
      get => createFormatFileCommand ??= new RelayCommand(() => CreateFormatFile(), () => CanExecute);
    }

    public ICommand ImportFormatFileCommand
    {
      get => importFormatFileCommand ??= new RelayCommand(() => ImportFormatTile(), () => CanExecute);
    }

    public ICommand FormatCodeCommand
    {
      get => formatCodeCommand ??= new RelayCommand(() => RunFormat(), () => CanExecute);
    }

    public ICommand OpenClangFormatUriCommand
    {
      get => openUri ??= new RelayCommand(() => WebsiteHandler.OpenUri("https://clangpowertools.com/blog/getting-started-with-clang-format-style-options.html"), () => CanExecute);
    }

    public ICommand ResetCommand
    {
      get => resetCommand ??= new RelayCommand(() => ResetOptionsAsync().SafeFireAndForget(), () => CanExecute);
    }

    public ICommand SelctCodeFileCommand
    {
      get => selctCodeFileCommand ??= new RelayCommand(() => ReadCodeFromFile(), () => CanExecute);
    }

    public ICommand ResetSearchCommand
    {
      get => resetSearchCommand ??= new RelayCommand(() => ResetSearchField(), () => CanExecute);
    }

    #endregion


    #region Public Methods

    public void PreviewDragOver(DragEventArgs e)
    {
      e.Handled = DropFileValidation(e, out string filePath);
      droppedFile = filePath;
    }

    public void PreviewDrop(DragEventArgs e)
    {
      if (droppedFile == null) return;

      using var streamReader = new StreamReader(droppedFile);
      configuratorView.CodeInput.Text = streamReader.ReadToEnd();
    }

    public void RunFormat()
    {
      if (windowLoaded == false) return;
      RunFormatAsync().SafeFireAndForget();
    }
    public void OpenMultipleInput(int index)
    {
      if (windowLoaded == false) return;
      CloseMultipleInput += FormatAfterClosingMultipleInput;
      SelectedOption = FormatOptions[index];
      OpenMultipleInput(SelectedOption, configuratorView);
    }

    public void FormatAfterClosingMultipleInput(object sender, EventArgs e)
    {
      SelectedOption.IsEnabled = true;
      RunFormatAsync().SafeFireAndForget();
      CloseMultipleInput -= FormatAfterClosingMultipleInput;
    }

    #endregion


    #region Private Methods

    private async Task RunFormatAsync()
    {
      if (IsAnyOptionEnabled() == false)
      {
        await SetOutputTextAsync(AppConstants.OutputCodeText);
        return;
      }
      var formatErrors = await FormatViewModelHelper.CheckOptionsValidityAsync(input, formatStyleOptions, selectedStyle);
      if (formatErrors.Item1)
      {
        await SetOutputTextAsync(formatErrors.Item2);
        return;
      }

      var documents = await DiffController.CreateFlowDocumentAsync(input, SelectedStyle, FormatOptions, new CancellationToken());
      await SetOutputLineNumberAsync(documents.Item3);
      configuratorView.CodeOutput.Document = documents.Item2;
    }


    private void InitializeStyleOptions(FormatOptionsAllData formatOptionsData)
    {
      formatOptionsData.DisableAllOptions();
      formatStyleOptions = formatOptionsData.GetFormatOptionsValues();
      selectedOption = formatStyleOptions.FirstOrDefault();
    }

    private void ChangeControlsDependingOnStyle()
    {
      switch (selectedStyle)
      {
        case FormatStyle.Custom:
          SetStyleControls("260", "80", FormatOptionsProvider.CustomOptionsData.GetFormatOptionsValues());
          break;
        case FormatStyle.LLVM:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.LlvmOptionsData.FormatOptions);
          break;
        case FormatStyle.Google:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.GoogleOptionsData.FormatOptions);
          break;
        case FormatStyle.Chromium:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.ChromiumOptionsData.FormatOptions);
          break;
        case FormatStyle.Mozilla:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.MozillaOptionsData.FormatOptions);
          break;
        case FormatStyle.WebKit:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.WebkitOptionsData.FormatOptions);
          break;
        case FormatStyle.Microsoft:
          SetStyleControls(nameColumnWidthMax, "0", FormatOptionsProvider.MicrosoftOptionsData.FormatOptions);
          break;
      }
    }

    private void SetStyleControls(string nameColumnWidth, string enableOptionColumnWidth, List<IFormatOption> options)
    {
      NameColumnWidth = nameColumnWidth;
      EnableOptionColumnWidth = enableOptionColumnWidth;
      FormatOptions = options;
      SelectedOption = FormatOptions.FirstOrDefault();
    }
    private void ReadCodeFromFile()
    {
      var filePath = OpenFile(string.Empty, ".cpp", AppConstants.CodeFileExtensions);

      if (File.Exists(filePath))
        configuratorView.CodeInput.Text = File.ReadAllText(filePath);
    }

    private async Task ResetOptionsAsync()
    {
      if (windowLoaded == false) return;
      await Task.Run(() =>
      {
        FormatOptionsProvider.ResetOptions();
        InitializeStyleOptions(FormatOptionsProvider.CustomOptionsData);
      });

      await SetOutputTextAsync(AppConstants.OutputCodeText);
      OnPropertyChanged(nameof(SelectedOption));
      OnPropertyChanged(nameof(FormatOptions));
    }

    private void CreateFormatFile()
    {
      string fileName = AppConstants.ClangFormat;
      string defaultExt = AppConstants.ClangFormat;
      string filter = AppConstants.ClangFormatExtension;

      string path = SaveFile(fileName, defaultExt, filter);
      if (string.IsNullOrEmpty(path) == false)
      {
        WriteContentToFile(path, FormatOptionFile.CreateOutput(formatStyleOptions, selectedStyle).ToString());
      }
    }

    private void ImportFormatTile()
    {
      string fileName = AppConstants.ClangFormat;
      string defaultExt = AppConstants.ClangFormat;
      string filter = AppConstants.ClangFormatExtension;

      string path = OpenFile(fileName, defaultExt, filter);
      if (string.IsNullOrEmpty(path) == false)
      {
        try
        {
          SelectedStyle = FormatStyle.Custom;
          ChangeControlsDependingOnStyle();

          var importer = new FormatOptionsImporter();
          importer.ImportFormatOptions(path);
          FormatOptions = FormatOptionsProvider.CustomOptionsData.GetFormatOptionsValues();
          SelectedOption = FormatOptions.First();

          RunFormat();
        }
        catch (Exception e)
        {
          MessageBox.Show(e.Message, "Clang-Format Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    }

    private void EditorLoaded(object sender, EventArgs e)
    {
      windowLoaded = true;
      configuratorView.Loaded -= EditorLoaded;
    }

    private static bool DropFileValidation(DragEventArgs e, out string droppedFile)
    {
      droppedFile = null;
      string[] droppedFiles = null;
      if (e.Data.GetDataPresent(DataFormats.FileDrop))
      {
        droppedFiles = e.Data.GetData(DataFormats.FileDrop, true) as string[];
      }

      if (droppedFiles == null || droppedFiles.Length != 1)
        return false;

      droppedFile = droppedFiles[0];
      return true;
    }

    private void ResetSearchField()
    {
      CheckSearch = string.Empty;
      ShowOptionDescription = true;

      OnPropertyChanged(nameof(FormatOptions));
    }

    private async Task FindFormatOptionsAsync(string search)
    {
      await Task.Run(() =>
      {
        searchResultFormatStyleOptions = formatStyleOptions.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        SelectedOption = searchResultFormatStyleOptions.FirstOrDefault();
        ShowOptionDescription = searchResultFormatStyleOptions.Count != 0;

        OnPropertyChanged(nameof(FormatOptions));
      });
    }

    private async Task SetInputLineNumberAsync(int numberOfLines)
    {
      InputLineNumber = await FormatViewModelHelper.GetLineNumbersAsync(numberOfLines);
    }

    private async Task SetOutputLineNumberAsync(int numberOfLines)
    {
      OutputLineNumber = await FormatViewModelHelper.GetLineNumbersAsync(numberOfLines);
    }

    private void OnPropertyChanged(string propertyName)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool IsAnyOptionEnabled()
    {
      foreach (var item in formatStyleOptions)
      {
        if (item.IsEnabled) return true;
      }
      return false;
    }

    private async Task SetOutputTextAsync(string text)
    {
      var paragraph = new Paragraph();
      paragraph.Inlines.Add(new Run(text));
      configuratorView.CodeOutput.Document.Blocks.Clear();
      configuratorView.CodeOutput.Document.Blocks.Add(paragraph);

      var output = AppConstants.OutputCodeText.Split(Environment.NewLine).Length;
      await SetOutputLineNumberAsync(output == 0 ? 1 : output);
    }

    #endregion
  }
}
