﻿using ClangFormatEditor.Enums;
using ClangFormatEditor.Interfaces;
using ClangFormatEditor.MVVM.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Process = System.Diagnostics.Process;

namespace ClangFormatEditor
{
  public class FormatEditorViewModel : CommonFormatEditorFunctionality, INotifyPropertyChanged, IFormatEditor
  {
    #region Members

    public event PropertyChangedEventHandler PropertyChanged;

    private readonly FormatEditorView formatEditorView;
    private ICommand selctCodeFileCommand;
    private ICommand createFormatFileCommand;
    private ICommand importFormatFileCommand;
    private ICommand formatCodeCommand;
    private ICommand resetCommand;
    private ICommand openUri;
    private ICommand resetSearchCommand;

    private string checkSearch = string.Empty;
    private bool showOptionDescription = true;
    private List<IFormatOption> searchResultFormatStyleOptions;
    private bool windowLoaded = false;
    private string nameColumnWidth;
    private string droppedFile;
    private const string nameColumnWidthMax = "340";

    #endregion

    #region Constructor

    public FormatEditorViewModel(FormatEditorView formatEditorView)
    {
      formatEditorView.Loaded += EditorLoaded;
      this.formatEditorView = formatEditorView;
      InitializeStyleOptions(FormatOptionsProvider.CustomOptionsData);
    }

    //Empty constructor used for XAML IntelliSense
    public FormatEditorViewModel()
    {

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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormatOptions)));
      }
    }

    public string CheckSearch
    {
      get
      {
        return checkSearch;
      }
      set
      {
        checkSearch = value;
        FindFormatOptionsAsync(checkSearch).SafeFireAndForget();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckSearch)));
      }
    }

    public IFormatOption SelectedOption
    {
      get
      {
        return selectedOption;
      }
      set
      {
        selectedOption = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
      }
    }

    public IEnumerable<FormatStyle> Styles
    {
      get
      {
        return Enum.GetValues(typeof(FormatStyle)).Cast<FormatStyle>();
      }
    }

    public IEnumerable<ToggleValues> BooleanComboboxValues
    {
      get
      {
        return Enum.GetValues(typeof(ToggleValues)).Cast<ToggleValues>();
      }
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
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedStyle)));

        RunFormat();
      }
    }

    public string NameColumnWidth
    {
      get
      {
        return nameColumnWidth;
      }
      set
      {
        nameColumnWidth = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NameColumnWidth)));
      }
    }

    public string EnableOptionColumnWidth
    {
      get
      {
        return nameColumnWidth;
      }
      set
      {
        nameColumnWidth = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableOptionColumnWidth)));
      }
    }

    public bool CanExecute
    {
      get
      {
        return true;
      }
    }

    public bool ShowOptionDescription
    {
      get
      {
        return showOptionDescription;
      }
      set
      {
        showOptionDescription = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowOptionDescription)));
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
      get => openUri ??= new RelayCommand(() => OpenUri("https://clangpowertools.com/blog/getting-started-with-clang-format-style-options.html"), () => CanExecute);
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

      using StreamReader streamReader = new StreamReader(droppedFile);
      formatEditorView.CodeEditor.Text = streamReader.ReadToEnd();
    }

    public void RunFormat()
    {
      if (windowLoaded == false) return;
      SetEditorOutputAfterFormat();
    }
    public void OpenMultipleInput(int index)
    {
      if (windowLoaded == false) return;
      CloseMultipleInput += FormatAfterClosingMultipleInput;
      SelectedOption = FormatOptions[index];
      OpenMultipleInput(SelectedOption, formatEditorView);
    }

    public void FormatAfterClosingMultipleInput(object sender, EventArgs e)
    {
      SelectedOption.IsEnabled = true;
      SetEditorOutputAfterFormat();
      CloseMultipleInput -= FormatAfterClosingMultipleInput;
    }

    public bool IsAnyOptionEnabled()
    {
      foreach (var item in formatStyleOptions)
      {
        if (item.IsEnabled) return true;
      }
      return false;
    }

    #endregion


    #region Private Methods

    private void SetEditorOutputAfterFormat()
    {
      formatEditorView.CodeEditorReadOnly.Text = RunFormat(formatEditorView.CodeEditor.Text);
    }

    private string RunFormat(string text)
    {
      var formatter = new StyleFormatter();
      var formattedText = formatter.FormatText(text, formatStyleOptions, selectedStyle);
      return formattedText;
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

    private static void OpenUri(string uri)
    {
      try
      {
        var processStartInfo = new ProcessStartInfo(uri)
        {
          UseShellExecute = true,
        };
        Process.Start(processStartInfo);
      }
      catch (Exception e)
      {
        MessageBox.Show(e.Message, "Clang Format Editor Failed", MessageBoxButton.OK, MessageBoxImage.Error);
      }
    }

    private void ReadCodeFromFile()
    {
      var filePath = OpenFile(string.Empty, ".cpp", AppConstants.CodeFileExtensions);

      if (File.Exists(filePath))
        formatEditorView.CodeEditor.Text = File.ReadAllText(filePath);
    }

    private async Task ResetOptionsAsync()
    {
      if (windowLoaded == false) return;
      await Task.Run(() =>
      {
        FormatOptionsProvider.ResetOptions();
        InitializeStyleOptions(FormatOptionsProvider.CustomOptionsData);
      });

      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormatOptions)));
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
          MessageBox.Show(e.Message, "Clang-Format Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
      }
    }

    private void EditorLoaded(object sender, EventArgs e)
    {
      windowLoaded = true;
      formatEditorView.Loaded -= EditorLoaded;
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
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormatOptions)));
    }

    private async Task FindFormatOptionsAsync(string search)
    {
      await Task.Run(() =>
    {
      searchResultFormatStyleOptions = formatStyleOptions.Where(e => e.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
      SelectedOption = searchResultFormatStyleOptions.FirstOrDefault();
      ShowOptionDescription = searchResultFormatStyleOptions.Count != 0;

      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FormatOptions)));
    });
    }

    #endregion
  }
}
