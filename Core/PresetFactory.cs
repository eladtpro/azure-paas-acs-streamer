using Microsoft.Azure.Management.Media.Models;

namespace RadioArchive
{
    public static class PresetFactory
    {
        public static Preset AacAudio => new StandardEncoderPreset
        {
            Codecs = {new CopyAudio(), new AacAudio {
                                Channels = 2,
                                SamplingRate = 48000,
                                Bitrate = 128000,
                                Profile = AacAudioProfile.AacLc,
                                Label = "aac-lc"
                            } },
            Formats = { new Mp4Format() }
        };

        public static Preset AdaptiveStreaming => new BuiltInStandardEncoderPreset()
        {
            // This sample uses the built-in encoding preset for Adaptive Bitrate Streaming.
            PresetName = EncoderNamedPreset.AdaptiveStreaming
        };

    }
}

