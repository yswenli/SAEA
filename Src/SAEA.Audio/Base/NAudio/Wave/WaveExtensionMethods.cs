using SAEA.Audio.Base.NAudio.Wave.SampleProviders;
using System;

namespace SAEA.Audio.Base.NAudio.Wave
{
    public static class WaveExtensionMethods
    {
        public static ISampleProvider ToSampleProvider(this IWaveProvider waveProvider)
        {
            return SampleProviderConverters.ConvertWaveProviderIntoSampleProvider(waveProvider);
        }

        public static void Init(this IWavePlayer wavePlayer, ISampleProvider sampleProvider, bool convertTo16Bit = false)
        {
            IWaveProvider arg_15_0;
            if (!convertTo16Bit)
            {
                IWaveProvider waveProvider = new SampleToWaveProvider(sampleProvider);
                arg_15_0 = waveProvider;
            }
            else
            {
                IWaveProvider waveProvider = new SampleToWaveProvider16(sampleProvider);
                arg_15_0 = waveProvider;
            }
            IWaveProvider waveProvider2 = arg_15_0;
            wavePlayer.Init(waveProvider2);
        }

        public static WaveFormat AsStandardWaveFormat(this WaveFormat waveFormat)
        {
            WaveFormatExtensible waveFormatExtensible = waveFormat as WaveFormatExtensible;
            if (waveFormatExtensible == null)
            {
                return waveFormat;
            }
            return waveFormatExtensible.ToStandardWaveFormat();
        }

        public static IWaveProvider ToWaveProvider(this ISampleProvider sampleProvider)
        {
            return new SampleToWaveProvider(sampleProvider);
        }

        public static IWaveProvider ToWaveProvider16(this ISampleProvider sampleProvider)
        {
            return new SampleToWaveProvider16(sampleProvider);
        }

        public static ISampleProvider FollowedBy(this ISampleProvider sampleProvider, ISampleProvider next)
        {
            return new ConcatenatingSampleProvider(new ISampleProvider[]
            {
                sampleProvider,
                next
            });
        }

        public static ISampleProvider FollowedBy(this ISampleProvider sampleProvider, TimeSpan silenceDuration, ISampleProvider next)
        {
            OffsetSampleProvider offsetSampleProvider = new OffsetSampleProvider(sampleProvider)
            {
                LeadOut = silenceDuration
            };
            return new ConcatenatingSampleProvider(new ISampleProvider[]
            {
                offsetSampleProvider,
                next
            });
        }

        public static ISampleProvider Skip(this ISampleProvider sampleProvider, TimeSpan skipDuration)
        {
            return new OffsetSampleProvider(sampleProvider)
            {
                SkipOver = skipDuration
            };
        }

        public static ISampleProvider Take(this ISampleProvider sampleProvider, TimeSpan takeDuration)
        {
            return new OffsetSampleProvider(sampleProvider)
            {
                Take = takeDuration
            };
        }

        public static ISampleProvider ToMono(this ISampleProvider sourceProvider, float leftVol = 0.5f, float rightVol = 0.5f)
        {
            if (sourceProvider.WaveFormat.Channels == 1)
            {
                return sourceProvider;
            }
            return new StereoToMonoSampleProvider(sourceProvider)
            {
                LeftVolume = leftVol,
                RightVolume = rightVol
            };
        }

        public static ISampleProvider ToStereo(this ISampleProvider sourceProvider, float leftVol = 1f, float rightVol = 1f)
        {
            if (sourceProvider.WaveFormat.Channels == 2)
            {
                return sourceProvider;
            }
            return new MonoToStereoSampleProvider(sourceProvider)
            {
                LeftVolume = leftVol,
                RightVolume = rightVol
            };
        }
    }
}
