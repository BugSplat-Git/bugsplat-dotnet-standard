using BugSplatDotNetStandard.Http;

namespace Tests
{
    internal class FakeS3ClientFactory: IS3ClientFactory
    {
        private IS3Client client;
        public FakeS3ClientFactory(IS3Client client)
        {
            this.client = client;
        }

        public IS3Client CreateClient()
        {
            return this.client;
        }
    }
}