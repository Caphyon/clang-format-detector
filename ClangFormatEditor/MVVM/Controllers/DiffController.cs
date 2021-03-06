﻿using ClangFormatEditor.DiffStyle;
using ClangFormatEditor.Enums;
using ClangFormatEditor.Helpers;
using ClangFormatEditor.Interfaces;
using ClangFormatEditor.MVVM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace ClangFormatEditor.MVVM.Controllers
{
  public class DiffController
  {
    #region Members

    private readonly StyleDetector styleDetector;

    #endregion

    #region Constructor

    public DiffController()
    {
      styleDetector = new StyleDetector();
    }

    #endregion

    #region Properties 

    public CancellationTokenSource CancellationSource { get; set; }

    public bool CancelTokenDisposed { get; set; }

    #endregion

    #region Public Methods

    public void CloseLoadDetectionView(object sender, EventArgs e)
    {
      if (CancelTokenDisposed == false)
      {
        CancellationSource.Cancel();
      }
    }

    public async Task<(FormatStyle matchedStyle, List<IFormatOption> matchedOptions)> GetFormatOptionsAsync(List<string> filesContent, CancellationToken cancelToken)
    {
      return await styleDetector.DetectStyleOptionsAsync(filesContent, cancelToken);
    }

    public static async Task<List<(FlowDocument, FlowDocument, int)>> CreateFlowDocumentsAsync(List<string> filesContent, FormatStyle formatStyle, List<IFormatOption> formatOptions, CancellationToken cancelToken)
    {
      var flowDocuments = new List<(FlowDocument, FlowDocument, int)>();

      foreach (var file in filesContent)
      {
        var documents = await CreateFlowDocumentAsync(file, formatStyle, formatOptions, cancelToken);
        flowDocuments.Add(documents);
      }
      return flowDocuments;
    }

    public static async Task<(FlowDocument, FlowDocument, int)> CreateFlowDocumentAsync(string input, FormatStyle formatStyle, List<IFormatOption> formatOptions, CancellationToken cancelToken)
    {
      var diffMatchPatchWrapper = new DiffMatchPatchWrapper();
      string output = string.Empty;
      await Task.Run(() =>
      {
        var formatter = new StyleFormatter();
        output = formatter.FormatText(input, formatOptions, formatStyle);
        diffMatchPatchWrapper.Diff(input, output);
        diffMatchPatchWrapper.CleanupSemantic();
      }, cancelToken);
      return diffMatchPatchWrapper.DiffAsFlowDocuments(input, output);
    }

    public static List<string> GetFileNames(List<string> filePaths)
    {
      var fileNames = new List<string>();
      foreach (var path in filePaths)
      {
        fileNames.Add(Path.GetFileName(path));
      }
      return fileNames;
    }

    public static void CopyOptionValues(IFormatOption optionToChange, IFormatOption optionToCopy)
    {
      switch (optionToChange)
      {
        case FormatOptionToggleModel toggleModel:
          toggleModel.BooleanCombobox = ((FormatOptionToggleModel)optionToCopy).BooleanCombobox;
          break;
        case FormatOptionInputModel inputModel:
          inputModel.Input = ((FormatOptionInputModel)optionToCopy).Input;
          break;
        case FormatOptionMultipleToggleModel multipleToggleModel:
          var defaultMultipleToggle = (FormatOptionMultipleToggleModel)optionToCopy;
          for (int i = 0; i < multipleToggleModel.ToggleFlags.Count; i++)
          {
            multipleToggleModel.ToggleFlags[i].Value = defaultMultipleToggle.ToggleFlags[i].Value;
          }
          break;
        case FormatOptionMultipleInputModel multipleInputModel:
          multipleInputModel.MultipleInput = ((FormatOptionMultipleInputModel)optionToCopy).MultipleInput;
          break;
        default:
          break;
      }
    }

    public static bool IsOptionChanged(IFormatOption option, IFormatOption defaultOption)
    {
      switch (option)
      {
        case FormatOptionToggleModel toggleModel:
          if (toggleModel.BooleanCombobox != ((FormatOptionToggleModel)defaultOption).BooleanCombobox)
          {
            return true;
          }
          break;
        case FormatOptionInputModel inputModel:
          if (inputModel.Input != ((FormatOptionInputModel)defaultOption).Input)
          {
            return true;
          }
          break;
        case FormatOptionMultipleToggleModel multipleToggleModel:
          var defaultMultipleToggle = (FormatOptionMultipleToggleModel)defaultOption;
          for (int i = 0; i < multipleToggleModel.ToggleFlags.Count; i++)
          {
            if (multipleToggleModel.ToggleFlags[i].Value != defaultMultipleToggle.ToggleFlags[i].Value)
            {
              return true;
            }
          }
          break;
        case FormatOptionMultipleInputModel multipleInputModel:
          if (multipleInputModel.MultipleInput != ((FormatOptionMultipleInputModel)defaultOption).MultipleInput)
          {
            return true;
          }
          break;
        default:
          break;
      }
      return false;
    }

    public static void DeleteFormatFolder()
    {
      string folderPath = Path.Combine(ProjectSetup.AppDataDirectory, AppConstants.FormatDirectory);
      FileSystem.DeleteDirectory(folderPath);
    }

    #endregion
  }
}
