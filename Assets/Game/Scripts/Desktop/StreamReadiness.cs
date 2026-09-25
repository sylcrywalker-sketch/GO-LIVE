namespace GoLive.Desktop
{
    // One preflight result serves both the presentation and the start command.
    public readonly struct StreamReadiness
    {
        public bool ChannelReady { get; }
        public bool InternetReady { get; }
        public bool MicrophoneReady { get; }
        public bool WebcamReady { get; }
        public bool QualitySupported { get; }
        public string ErrorKey { get; }
        public bool CanStart => ErrorKey == null;

        internal StreamReadiness(bool channelReady, bool internetReady, bool microphoneReady,
            bool webcamReady, bool qualitySupported, string errorKey)
        {
            ChannelReady = channelReady;
            InternetReady = internetReady;
            MicrophoneReady = microphoneReady;
            WebcamReady = webcamReady;
            QualitySupported = qualitySupported;
            ErrorKey = errorKey;
        }
    }
}
