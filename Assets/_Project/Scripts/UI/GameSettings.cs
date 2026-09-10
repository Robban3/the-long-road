using UnityEngine;

namespace TheVeil.UI
{
    /// <summary>
    /// The player's own switches, kept on the device beside the save.
    ///
    /// Apart from the campaign on purpose. Wiping progress is a thing a player does to
    /// start the story again; it is not a request to be put back into a language they
    /// cannot read or to have the sound come back on in a meeting.
    /// </summary>
    public static class GameSettings
    {
        const string SoundKey = "theveil.sound";

        /// <summary>
        /// Whether the game makes any sound at all. One switch rather than music and
        /// effects apart, because there is no sound yet to set a balance between; the
        /// switch is here so that the first sound added arrives already obeying it.
        /// </summary>
        public static bool Sound
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) != 0;
            set
            {
                PlayerPrefs.SetInt(SoundKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Apply();
            }
        }

        /// <summary>
        /// Puts the switches into effect. The listener's volume is global and outlives a
        /// scene load, so the menu applying it once as it opens covers the whole session.
        /// </summary>
        public static void Apply() => AudioListener.volume = Sound ? 1f : 0f;
    }
}
