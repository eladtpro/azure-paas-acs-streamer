using Microsoft.Azure.Management.Media.Models;

namespace RadioArchive
{
    public static class PresetFactory
    {
        public static Preset Preset(PresetProfile profile)
        {
            return profile switch
            {
                PresetProfile.AdaptiveStreaming => new BuiltInStandardEncoderPreset(presetName: EncoderNamedPreset.AdaptiveStreaming),
                PresetProfile.AacAudio => new StandardEncoderPreset(
                    codecs: new Codec[]{
                        new CopyAudio(),
                        new AacAudio {
                            Channels = 2,
                            SamplingRate = 48000,
                            Bitrate = 128000,
                            Profile = AacAudioProfile.AacLc,
                            Label = "aac-lc"
                        }
                    },
                    formats: new Format[]{
                        new Mp4Format(
                            filenamePattern: "{Basename}-{Label}-{Bitrate}.{Extension}")
                    }
                ),
                _ => throw new ArgumentOutOfRangeException(nameof(profile), $"Not expected profile (PresetProfile) value: {profile}")
            };
        }
    }
}

