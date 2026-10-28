using System;
using System.Globalization;
using System.Speech.Synthesis;

namespace LazyTurtle
{
    /// <summary>
    /// Reads questions aloud for children who cannot read yet, using a Windows voice in the game's
    /// language (if one is installed - English voices ship with Windows 7 and 11, Polish ones with a
    /// Polish Windows 10/11). Without a matching voice it stays silent rather than mispronouncing.
    /// </summary>
    sealed class Speech : IDisposable
    {
        SpeechSynthesizer synth;
        public bool Enabled = true;

        public Speech()
        {
            try
            {
                synth = new SpeechSynthesizer();
                synth.Rate = -1;
            }
            catch (Exception)
            {
                synth = null;
            }
        }

        /// <summary>True when a voice for the current game language is installed.</summary>
        public bool Available
        {
            get
            {
                if (synth == null) return false;
                try
                {
                    foreach (var v in synth.GetInstalledVoices())
                        if (v.Enabled && v.VoiceInfo.Culture.TwoLetterISOLanguageName == (L.Pl ? "pl" : "en")) return true;
                }
                catch (Exception) { }
                return false;
            }
        }

        public void Say(string text)
        {
            if (!Enabled || synth == null || string.IsNullOrEmpty(text)) return;
            try
            {
                synth.SpeakAsyncCancelAll();
                synth.SelectVoiceByHints(VoiceGender.NotSet, VoiceAge.NotSet, 0, new CultureInfo(L.Pl ? "pl-PL" : "en-US"));
                if (synth.Voice.Culture.TwoLetterISOLanguageName != (L.Pl ? "pl" : "en")) return;
                synth.SpeakAsync(text);
            }
            catch (Exception) { }
        }

        public void Stop()
        {
            try { synth?.SpeakAsyncCancelAll(); }
            catch (Exception) { }
        }

        public void Dispose()
        {
            Stop();
            synth?.Dispose();
            synth = null;
        }
    }
}
