namespace Solicen.Localization.UE4
{
    internal class UnrealFormat
    {
        #region Texture Format
        public class Texture
        {
            /// <summary>
            /// Определение текстуры формата PF_DXT5.
            /// </summary>
            public static readonly byte[] PF_DXT5 = [0x50, 0x46, 0x5F, 0x44, 0x58, 0x54, 0x35];

            /// <summary>
            /// Определение текстуры формата B8G8R8A8.
            /// </summary>
            public static readonly byte[] B8G8R8A8 = [0x42, 0x38, 0x47, 0x38, 0x52, 0x38, 0x41, 0x38];
        }
        #endregion

        #region Table Format
        public class Table
        {
            /// <summary>
            /// Переведенная в байты строка StringTable.
            /// </summary>
            public static readonly byte[] StringTable = [0x53, 0x74, 0x72, 0x69, 0x6E, 0x67, 0x54, 0x61, 0x62, 0x6C, 0x65];
            public static readonly byte[] ST_Exp = [0x00,0x22,0x00,0x80];

            /// <summary>
            /// Переведенная в байты строка DataTable.
            /// </summary>
            public static readonly byte[] DataTable = [0x44, 0x61, 0x74, 0x61, 0x54, 0x61, 0x62, 0x6C, 0x65];
            public static readonly byte[] DT_Exp = ST_Exp = [0x00, 0x22, 0x04, 0x80];

            /// <summary>
            /// Байтовое определение таблицы, может быть представлено, как StringTable, так и DataTable.
            /// </summary>
            public static readonly byte[] AnyTable = [0x00, 0x00, 0x00, 0x00, 0x00, 0x64, 0xC1, 0x00, 0x00, 0x00, 0x00];
        }
        #endregion

        #region Property
        public class Property
        {
            public static readonly byte[] ArrayProperty = [0x41, 0x72, 0x72, 0x61, 0x79, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79];
            public static readonly byte[] TextProperty = [0x54, 0x65, 0x78, 0x74, 0x50, 0x72, 0x6F, 0x70, 0x65, 0x72, 0x74, 0x79];
        }
        #endregion
    }
}
