namespace NbCore
{
    public struct NbMeshBufferInfo
    {
        [NbSerializable] public uint semantic;
        [NbSerializable] public NbPrimitiveDataType type;
        [NbSerializable] public int count;
        [NbSerializable] public int stride;
        [NbSerializable] public int offset;
        [NbSerializable] public string sem_text;
        [NbSerializable] public bool normalize;

        public NbMeshBufferInfo(uint sem, NbPrimitiveDataType typ, int c, int s, int off, string t, bool n)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
            normalize = n;
            offset = off;
        }
    }
}
