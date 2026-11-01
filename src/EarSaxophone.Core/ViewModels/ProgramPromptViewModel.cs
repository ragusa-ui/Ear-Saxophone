using EarSaxophone.Core.Audio;
using EarSaxophone.Core.Localization;
using EarSaxophone.Core.Midi;

namespace EarSaxophone.Core.ViewModels;

public enum ProgramPromptAnswer
{
    /// <summary>The program was put on a control.</summary>
    Assigned,

    /// <summary>"Nicht jetzt", or no reaction within <see cref="ProgramPromptViewModel.AutoDismissAfter"/>.</summary>
    NotNow,

    /// <summary>"Nie fragen": the program goes on the ignore list.</summary>
    NeverAsk,
}

/// <summary>
/// The small question "&lt;Program&gt; spielt Ton – auf welchen Regler?" (design V2, section 6). The App shows it in a window
/// that takes no focus and closes it when <see cref="Closed"/> is raised. Without reaction the main view model answers
/// <see cref="NotNow"/> after <see cref="AutoDismissAfter"/> (checked on its one-second tick). Moving a fader or knob at
/// the device answers it as well (the main view model forwards that).
/// </summary>
public sealed class ProgramPromptViewModel : ObservableObject
{
    public static readonly TimeSpan AutoDismissAfter = TimeSpan.FromSeconds(25);

    private readonly Action<ProgramPromptViewModel, ProgramPromptAnswer, ControlId?> _answer;
    private bool _isOpen = true;

    internal ProgramPromptViewModel(ProgramPrompt prompt, IReadOnlyList<ControllerElementViewModel> choices, Action<ProgramPromptViewModel, ProgramPromptAnswer, ControlId?> answer)
    {
        Prompt = prompt;
        Choices = choices;
        _answer = answer;
        AssignCommand = new RelayCommand(p => Assign((p as ControllerElementViewModel)?.Control), _ => _isOpen);
        NotNowCommand = new RelayCommand(_ => NotNow(), _ => _isOpen);
        NeverAskCommand = new RelayCommand(_ => NeverAsk(), _ => _isOpen);
    }

    public ProgramPrompt Prompt { get; }

    public string ExeName => Prompt.ExeName;

    /// <summary>Full path of the program when known (for its icon).</summary>
    public string? ExePath => Prompt.ExePath;

    public string Title => Texts.PromptPlaysSound(Prompt.ExeName);

    public string Question => Texts.PromptQuestion;

    public string NotNowText => Texts.PromptNotNow;

    public string NeverAskText => Texts.PromptNeverAsk;

    /// <summary>The faders and knobs that can take the program (the mini picture; their badges show what is on them).</summary>
    public IReadOnlyList<ControllerElementViewModel> Choices { get; }

    /// <summary>Parameter: the chosen <see cref="ControllerElementViewModel"/>.</summary>
    public RelayCommand AssignCommand { get; }

    public RelayCommand NotNowCommand { get; }

    public RelayCommand NeverAskCommand { get; }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>Raised once when the question is answered in any way; the App closes its window.</summary>
    public event Action<ProgramPromptAnswer>? Closed;

    /// <summary>Puts the program on the control; only faders and knobs offered in <see cref="Choices"/> are accepted.</summary>
    public bool Assign(ControlId? control)
    {
        if (control is not { } chosen || !Choices.Any(c => c.Control == chosen))
        {
            return false;
        }

        return Finish(ProgramPromptAnswer.Assigned, chosen);
    }

    public void NotNow() => Finish(ProgramPromptAnswer.NotNow, null);

    public void NeverAsk() => Finish(ProgramPromptAnswer.NeverAsk, null);

    private bool Finish(ProgramPromptAnswer answer, ControlId? control)
    {
        if (!_isOpen)
        {
            return false;
        }

        IsOpen = false;
        AssignCommand.RaiseCanExecuteChanged();
        NotNowCommand.RaiseCanExecuteChanged();
        NeverAskCommand.RaiseCanExecuteChanged();
        _answer(this, answer, control);
        Closed?.Invoke(answer);
        return true;
    }
}
