using System.Windows.Input;

namespace OpenVisionLab.Mvvm
{
    public sealed class KeyInputCommandArgs
    {
        public KeyInputCommandArgs(Key key, ModifierKeys modifiers, bool isRepeat, object originalSource = null)
        {
            Key = key;
            Modifiers = modifiers;
            IsRepeat = isRepeat;
            OriginalSource = originalSource;
        }

        public Key Key { get; }

        public ModifierKeys Modifiers { get; }

        public bool IsRepeat { get; }

        public object OriginalSource { get; }

        public bool Handled { get; set; }
    }
}
