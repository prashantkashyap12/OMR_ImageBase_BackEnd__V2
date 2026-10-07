namespace SQCScanner.Services
{
    public class OmrProcessingControlService
    {
        // globle state me kaam karta hai, Synchronization mechanism implement karna
        private readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true); 

        private bool _stopRequested = false;
        public bool IsProcessingPaused => !_pauseEvent.IsSet;
        public bool IsStopRequested => _stopRequested;


        public void PauseProcessing()
        {
            _pauseEvent.Reset(); 
        }

        public void ResumeProcessing()
        {
            _pauseEvent.Set();
        }

        public void StopProcessing()
        {
            _stopRequested = true;
            _pauseEvent.Set();
        }

        public void ResetProcessing()
        {
            _stopRequested = false;
            _pauseEvent.Set();
        }

        public void WaitIfPaused()
        {
            _pauseEvent.Wait(); 
        }
    }
}
