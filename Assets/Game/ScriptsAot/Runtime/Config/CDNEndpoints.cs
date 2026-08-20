namespace Game.Aot
{
    public readonly struct CDNEndpoints
    {
        public readonly string MainURL;

        public CDNEndpoints(string mainURL)
        {
            MainURL = mainURL;
        }

        public static readonly CDNEndpoints Empty = new(string.Empty);
    }
}
