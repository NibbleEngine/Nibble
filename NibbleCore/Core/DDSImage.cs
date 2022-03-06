using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NbCore {

	//This class should be abstracted and based on the game we should provide
	//different loaders since everyone is using completely custom assets.
	//This is created based on NMS DDS format
	public class DDSImage : NbTextureData
	{
		private const int DDPF_ALPHAPIXELS = 0x00000001;
		private const int DDPF_ALPHA = 0x00000002;
		private const int DDPF_FOURCC = 0x00000004;
		private const int DDPF_RGB = 0x00000040;
		private const int DDPF_YUV = 0x00000200;
		private const int DDPF_LUMINANCE = 0x00020000;
		private const int DDSD_MIPMAPCOUNT = 0x00020000;
		private const int FOURCC_DXT1 = 0x31545844;
		private const int FOURCC_DX10 = 0x30315844;
		private const int FOURCC_DXT5 = 0x35545844;

		public int dwMagic;
		public DDS_HEADER header = new DDS_HEADER();
		public DDS_HEADER_DXT10 header10 = null;//If the DDS_PIXELFORMAT dwFlags is set to DDPF_FOURCC and dwFourCC is set to "DX10"
		//public byte[] bdata2;//pointer to an array of bytes that contains the remaining surfaces such as; mipmap levels, faces in a cube map, depths in a volume texture.
	    public List<byte[]> mipMaps = new List<byte[]>(); //TODO load and upload them separately.
		public int blockSize = 16;

	    public DDSImage(byte[] rawdata) {
            using MemoryStream ms = new MemoryStream(rawdata); using (BinaryReader r = new BinaryReader(ms))
			{
				dwMagic = r.ReadInt32();
				if (dwMagic != 0x20534444)
				{
					throw new Exception("This is not a DDS!");
				}

				header = ReadHeader(r);

				if (((header.ddspf.dwFlags & DDPF_FOURCC) != 0) && (header.ddspf.dwFourCC == FOURCC_DX10 /*DX10*/))
				{
					header10 = Read_DDS_HEADER10(r);
					//Override PitchOrLinearSize in case of BC7 textures
					switch (header10.dxgiFormat)
					{
						case DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM:
							header.dwPitchOrLinearSize = header.dwWidth * header.dwHeight;
							break;
						default:
							break;
					}
				}

				Data = r.ReadBytes((int)(r.BaseStream.Length - r.BaseStream.Position)); //Read everything

			}

			//Save Attributes
			Width = header.dwWidth;
			Height = header.dwHeight;
			Depth = header.dwDepth;
			MipMapCount = header.dwMipMapCount;
			blockSize = 16;

			if (header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_VOLUME))
            {
				Depth = header.dwDepth;
				target = NbTextureTarget.Texture2DArray;
				Faces = 1;
			} else if (header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_POSITIVEX) ||
					   header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_NEGATIVEX) ||
					   header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_POSITIVEY) ||
					   header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_NEGATIVEY) ||
					   header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_POSITIVEZ) ||
					   header.dwCaps2.HasFlag(DDSCAPS2.DDSCAPS2_CUBEMAP_NEGATIVEZ))
            {
				Common.Callbacks.Assert(false, "Inconsistent flag");
				
            } else
            {
				if (Depth > 1)
					target = NbTextureTarget.Texture2DArray;
				else if (Depth == 0)
					Common.Callbacks.Assert(false, "Zero Depth Investigate...");
				else
					target = NbTextureTarget.Texture2D;

				Faces = Depth;
				Depth = 0;
			}
			
			bool compressed = false;
			switch (header.ddspf.dwFourCC)
			{
				//DXT1
				case (0x31545844):
					pif = NbTextureInternalFormat.DXT1;
					compressed = true;
					blockSize = 8;
					break;
				//DXT5
				case (0x35545844):
					pif = NbTextureInternalFormat.DXT5;
					compressed = true;
					break;
				//ATI2A2XY
				case (0x32495441):
					pif = NbTextureInternalFormat.RGTC2; //Normal maps are probably never srgb
					compressed = true;
					break;
				//DXT10 HEADER
				case (0x30315844):
					{
						switch (header10.dxgiFormat)
						{
							case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
								pif = NbTextureInternalFormat.BC7;
								compressed = true;
								break;
							default:
								throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
						}
						break;
					}
				default:
					throw new ApplicationException($"Unimplemented Pixel format {header.ddspf.dwFourCC}");
			}

		}

		public override MemoryStream Export()
		{
			MemoryStream ms = new MemoryStream();

			using (BinaryWriter r = new BinaryWriter(ms, Encoding.Default, true))
            {
				r.Write(dwMagic);
				WriteHeader(r);

				if (((header.ddspf.dwFlags & DDPF_FOURCC) != 0) && (header.ddspf.dwFourCC == FOURCC_DX10 /*DX10*/))
				{
					WriteHeader10(r);
				}

				r.Write(Data);
			}

			return ms;
		}

		//private Bitmap readBlockImage(byte[] data, int w, int h) {
		//	switch (header.ddspf.dwFourCC) {
		//		case FOURCC_DXT1:
		//			return UncompressDXT1(data, w, h);
		//		case FOURCC_DXT5:
		//			return UncompressDXT5(data, w, h);
		//		default: break;
		//	}
		//	throw new Exception(string.Format("0x{0} texture compression not implemented.", header.ddspf.dwFourCC.ToString("X")));
		//}

		//#region DXT1
		//private Bitmap UncompressDXT1(byte[] data, int w, int h) {
		//	Bitmap res = new Bitmap((w < 4) ? 4 : w, (h < 4) ? 4 : h);
		//	using (MemoryStream ms = new MemoryStream(data)) {
		//		using (BinaryReader r = new BinaryReader(ms)) {
		//			for (int j = 0; j < h; j += 4) {
		//				for (int i = 0; i < w; i += 4) {
		//					DecompressBlockDXT1(i, j, r.ReadBytes(8), res);
		//				}
		//			}
		//		}
		//	}
		//	return res;
		//}

		//private void DecompressBlockDXT1(int x, int y, byte[] blockStorage, Bitmap image) {
		//	ushort color0 = (ushort)(blockStorage[0] | blockStorage[1] << 8);
		//	ushort color1 = (ushort)(blockStorage[2] | blockStorage[3] << 8);

		//	int temp;

		//	temp = (color0 >> 11) * 255 + 16;
		//	byte r0 = (byte)((temp / 32 + temp) / 32);
		//	temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
		//	byte g0 = (byte)((temp / 64 + temp) / 64);
		//	temp = (color0 & 0x001F) * 255 + 16;
		//	byte b0 = (byte)((temp / 32 + temp) / 32);

		//	temp = (color1 >> 11) * 255 + 16;
		//	byte r1 = (byte)((temp / 32 + temp) / 32);
		//	temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
		//	byte g1 = (byte)((temp / 64 + temp) / 64);
		//	temp = (color1 & 0x001F) * 255 + 16;
		//	byte b1 = (byte)((temp / 32 + temp) / 32);

		//	uint code = (uint)(blockStorage[4] | blockStorage[5] << 8 | blockStorage[6] << 16 | blockStorage[7] << 24);

		//	for (int j = 0; j < 4; j++) {
		//		for (int i = 0; i < 4; i++) {
		//			Color finalColor = Color.FromArgb(0);
		//			byte positionCode = (byte)((code >> 2 * (4 * j + i)) & 0x03);

		//			if (color0 > color1) {
		//				switch (positionCode) {
		//					case 0:
		//						finalColor = Color.FromArgb(255, r0, g0, b0);
		//						break;
		//					case 1:
		//						finalColor = Color.FromArgb(255, r1, g1, b1);
		//						break;
		//					case 2:
		//						finalColor = Color.FromArgb(255, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
		//						break;
		//					case 3:
		//						finalColor = Color.FromArgb(255, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
		//						break;
		//				}
		//			} else {
		//				switch (positionCode) {
		//					case 0:
		//						finalColor = Color.FromArgb(255, r0, g0, b0);
		//						break;
		//					case 1:
		//						finalColor = Color.FromArgb(255, r1, g1, b1);
		//						break;
		//					case 2:
		//						finalColor = Color.FromArgb(255, (r0 + r1) / 2, (g0 + g1) / 2, (b0 + b1) / 2);
		//						break;
		//					case 3:
		//						finalColor = Color.FromArgb(255, 0, 0, 0);
		//						break;
		//				}
		//			}

		//			image.SetPixel(x + i, y + j, finalColor);
		//		}
		//	}
		//}
		//#endregion
		//#region DXT5
		//private Bitmap UncompressDXT5(byte[] data, int w, int h) {
		//	Bitmap res = new Bitmap((w < 4) ? 4 : w, (h < 4) ? 4 : h);
		//	using (MemoryStream ms = new MemoryStream(data)) {
		//		using (BinaryReader r = new BinaryReader(ms)) {
		//			for (int j = 0; j < h; j += 4) {
		//				for (int i = 0; i < w; i += 4) {
		//					DecompressBlockDXT5(i, j, r.ReadBytes(16), res);
		//				}
		//			}
		//		}
		//	}
		//	return res;
		//}

		//void DecompressBlockDXT5(int x, int y, byte[] blockStorage, Bitmap image) {
		//	byte alpha0 = blockStorage[0];
		//	byte alpha1 = blockStorage[1];

		//	int bitOffset = 2;
		//	uint alphaCode1 = (uint)(blockStorage[bitOffset + 2] | (blockStorage[bitOffset + 3] << 8) | (blockStorage[bitOffset + 4] << 16) | (blockStorage[bitOffset + 5] << 24));
		//	ushort alphaCode2 = (ushort)(blockStorage[bitOffset + 0] | (blockStorage[bitOffset + 1] << 8));

		//	ushort color0 = (ushort)(blockStorage[8] | blockStorage[9] << 8);
		//	ushort color1 = (ushort)(blockStorage[10] | blockStorage[11] << 8);

		//	int temp;

		//	temp = (color0 >> 11) * 255 + 16;
		//	byte r0 = (byte)((temp / 32 + temp) / 32);
		//	temp = ((color0 & 0x07E0) >> 5) * 255 + 32;
		//	byte g0 = (byte)((temp / 64 + temp) / 64);
		//	temp = (color0 & 0x001F) * 255 + 16;
		//	byte b0 = (byte)((temp / 32 + temp) / 32);

		//	temp = (color1 >> 11) * 255 + 16;
		//	byte r1 = (byte)((temp / 32 + temp) / 32);
		//	temp = ((color1 & 0x07E0) >> 5) * 255 + 32;
		//	byte g1 = (byte)((temp / 64 + temp) / 64);
		//	temp = (color1 & 0x001F) * 255 + 16;
		//	byte b1 = (byte)((temp / 32 + temp) / 32);

		//	uint code = (uint)(blockStorage[12] | blockStorage[13] << 8 | blockStorage[14] << 16 | blockStorage[15] << 24);

		//	for (int j = 0; j < 4; j++) {
		//		for (int i = 0; i < 4; i++) {
		//			ushort alphaCodeIndex = (ushort) (3 * (4 * j + i));
		//			ushort alphaCode;

		//			if (alphaCodeIndex <= 12) {
		//				alphaCode = (ushort) ((alphaCode2 >> alphaCodeIndex) & 0x07);
		//			} else if (alphaCodeIndex == 15) {
		//				alphaCode = (ushort) ((alphaCode2 >> 15) | ((alphaCode1 << 1) & 0x06));
		//			} else {
		//				alphaCode = (ushort) ((alphaCode1 >> (alphaCodeIndex - 16)) & 0x07);
		//			}

		//			byte finalAlpha;
		//			if (alphaCode == 0) {
		//				finalAlpha = alpha0;
		//			} else if (alphaCode == 1) {
		//				finalAlpha = alpha1;
		//			} else {
		//				if (alpha0 > alpha1) {
		//					finalAlpha = (byte)(((8 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 7);
		//				} else {
		//					if (alphaCode == 6)
		//						finalAlpha = 0;
		//					else if (alphaCode == 7)
		//						finalAlpha = 255;
		//					else
		//						finalAlpha = (byte)(((6 - alphaCode) * alpha0 + (alphaCode - 1) * alpha1) / 5);
		//				}
		//			}

		//			byte colorCode = (byte)((code >> 2 * (4 * j + i)) & 0x03);

		//			Color finalColor = new Color();
		//			switch (colorCode) {
		//				case 0:
		//					finalColor = Color.FromArgb(finalAlpha, r0, g0, b0);
		//					break;
		//				case 1:
		//					finalColor = Color.FromArgb(finalAlpha, r1, g1, b1);
		//					break;
		//				case 2:
		//					finalColor = Color.FromArgb(finalAlpha, (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3);
		//					break;
		//				case 3:
		//					finalColor = Color.FromArgb(finalAlpha, (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3);
		//					break;
		//			}
		//			image.SetPixel(x + i, y + j, finalColor);
		//		}
		//	}
		//}
		//#endregion

		//#region V8U8
		//private Bitmap UncompressV8U8(byte[] data, int w, int h) {
		//	Bitmap res = new Bitmap(w, h);
		//	using (MemoryStream ms = new MemoryStream(data)) {
		//		using (BinaryReader r = new BinaryReader(ms)) {
		//			for (int y = 0; y < h; y++) {
		//				for (int x = 0; x < w; x++) {
		//					sbyte red = r.ReadSByte();
		//					sbyte green = r.ReadSByte();
		//					byte blue = 0xFF;

		//					res.SetPixel(x, y, Color.FromArgb(0x7F - red, 0x7F - green, blue));
		//				}
		//			}
		//		}
		//	}
		//	return res;
		//}
		//#endregion

		//private Bitmap readLinearImage(byte[] data, int w, int h) {
		//	Bitmap res = new Bitmap(w, h);
		//	using (MemoryStream ms = new MemoryStream(data)) {
		//		using (BinaryReader r = new BinaryReader(ms)) {
		//			for (int y = 0; y < h; y++) {
		//				for (int x = 0; x < w; x++) {
		//					res.SetPixel(x, y, Color.FromArgb(r.ReadInt32()));
		//				}
		//			}
		//		}
		//	}
		//	return res;
		//}

		public int getLayerSize()
        {
			int size = 0;
			int w = Width;
			int h = Height;
			for (int i = 0; i < MipMapCount; i++)
            {
				size += System.Math.Max(w * h * blockSize / 16, blockSize);
				w /= 2;
				h /= 2;
			}
			return size;
        }

		public static bool ReplaceTextureLayer(DDSImage new_image, DDSImage target, int depth_id)
		{
			//At first do a sanity check between the two images

			if (new_image.header.dwMipMapCount != target.header.dwMipMapCount)
			{
				//Callbacks.Logger.Log($"Incorrect MipmapCount for the new image. Correct Value : { target.header.dwMipMapCount}", LogVerbosityLevel.WARNING);
				return false;
			}

			if (new_image.header.dwWidth != target.header.dwWidth)
			{
				//Callbacks.Logger.Log($"Incorrect Width for the new image. Correct Value : { target.header.dwWidth}", LogVerbosityLevel.WARNING);
				return false;
			}

			if (new_image.header.dwHeight != target.header.dwHeight)
			{
				//Callbacks.Logger.Log($"Incorrect Height for the new image. Correct Value : { target.header.dwHeight}", LogVerbosityLevel.WARNING);
				return false;
			}

			if (new_image.header.ddspf.dwFourCC != target.header.ddspf.dwFourCC)
			{
				//Callbacks.Logger.Log($"Incorrect FORMAT for the new image. Correct Value : { target.header.ddspf.dwFourCC}", LogVerbosityLevel.WARNING);
				return false;
			}

			switch (new_image.header.ddspf.dwFourCC)
			{
				//DXT1
				case (0x31545844):
					break;
			}

			int new_image_offset = 0;
			int target_image_offset = 0;
			int temp_size = target.header.dwPitchOrLinearSize;
			int depth_count = target.header.dwDepth;
			int w = new_image.header.dwWidth;
			int h = new_image.header.dwHeight;
			for (int i = 0; i < target.header.dwMipMapCount; i++)
			{
				target_image_offset += temp_size * depth_id;
				byte[] temp_data = new byte[temp_size];
				Buffer.BlockCopy(new_image.Data, new_image_offset, temp_data, 0, temp_size);
				Buffer.BlockCopy(temp_data, 0, target.Data, target_image_offset, temp_size);

				target_image_offset += temp_size * (depth_count - depth_id);
				new_image_offset += temp_size;

				w = System.Math.Max(w >> 1, 1);
				h = System.Math.Max(h >> 1, 1);

				temp_size = System.Math.Max(1, (w + 3) / 4) * System.Math.Max(1, (h + 3) / 4) * target.blockSize;
				//This works only for square textures
				//temp_size = Math.Max(temp_size/4, blocksize);
			}

			return true;
		}


		private DDS_HEADER ReadHeader(BinaryReader r) {

			DDS_HEADER h = new DDS_HEADER()
			{
				dwSize = r.ReadInt32(),
				dwFlags = (DWFLAGS) r.ReadInt32(),
				dwHeight = r.ReadInt32(),
				dwWidth = r.ReadInt32(),
				dwPitchOrLinearSize = r.ReadInt32(),
				dwDepth = System.Math.Max(r.ReadInt32(), 1),
				dwMipMapCount = r.ReadInt32()
			};

			for (int i = 0; i < 11; ++i)
				h.dwReserved1[i] = r.ReadInt32();

			h.ddspf = ReadPixelFormat(r);
			h.dwCaps = (DDSCAPS) r.ReadInt32();
			h.dwCaps2 = (DDSCAPS2) r.ReadInt32();
			h.dwCaps3 = r.ReadInt32();
			h.dwCaps4 = r.ReadInt32();
			h.dwReserved2 = r.ReadInt32();

			return h;
		}

		private void WriteHeader(BinaryWriter bw)
		{
			bw.Write(header.dwSize);
			bw.Write((int) header.dwFlags);
			bw.Write(header.dwHeight);
			bw.Write(header.dwWidth);
            //Calculate pitch
            int pitch = System.Math.Max(1, ((header.dwWidth + 3) / 4)) * blockSize;
			bw.Write(pitch);
			bw.Write(header.dwDepth);
			bw.Write(header.dwMipMapCount);

			for (int i = 0; i < 11; ++i)
				bw.Write(header.dwReserved1[i]);

			WritePixelFormat(bw);

			bw.Write((int) header.dwCaps);
			bw.Write((int) header.dwCaps2);
			bw.Write(header.dwCaps3);
			bw.Write(header.dwCaps4);
			bw.Write(header.dwReserved2);
		}
		
		private DDS_HEADER_DXT10 Read_DDS_HEADER10(BinaryReader r)
        {
			DDS_HEADER_DXT10 h = new()
			{
				dxgiFormat = (DXGI_FORMAT) r.ReadInt32(),
				resourceDimension = (D3D10_RESOURCE_DIMENSION) r.ReadInt32(),
				miscFlag = r.ReadUInt32(),
				arraySize = r.ReadUInt32(),
				miscFlags2 = r.ReadUInt32()
			};
			return h;
        }

		private void WriteHeader10(BinaryWriter bw)
        {
			bw.Write((int) header10.dxgiFormat);
			bw.Write((int) header10.resourceDimension);
			bw.Write((int) header10.miscFlag);
			bw.Write((int) header10.arraySize);
			bw.Write((int) header10.miscFlags2);
		}

		private DDS_PIXELFORMAT ReadPixelFormat(BinaryReader r) {
			DDS_PIXELFORMAT p = new DDS_PIXELFORMAT();
			p.dwSize = r.ReadInt32();
			p.dwFlags = r.ReadInt32();
			p.dwFourCC = r.ReadInt32();

			switch (p.dwFourCC)
			{
				//DXT1
				case (0x31545844):
					blockSize = 8;
					break;
				default:
					blockSize = 16;
					break;
			}

			p.dwRGBBitCount = r.ReadInt32();
			p.dwRBitMask = r.ReadInt32();
			p.dwGBitMask = r.ReadInt32();
			p.dwBBitMask = r.ReadInt32();
			p.dwABitMask = r.ReadInt32();
			return p;
		}

		private void WritePixelFormat(BinaryWriter bw)
        {
			bw.Write(header.ddspf.dwSize);
			bw.Write(header.ddspf.dwFlags);
			bw.Write(header.ddspf.dwFourCC);
			bw.Write(header.ddspf.dwRGBBitCount);
			bw.Write(header.ddspf.dwRBitMask);
			bw.Write(header.ddspf.dwGBitMask);
			bw.Write(header.ddspf.dwBBitMask);
			bw.Write(header.ddspf.dwABitMask);
		}
	
		public byte[] GetMipMapData(int depth, int mip_id)
        {
			//Calculate mipmap_size
			int offset = 0;
			int temp_size = header.dwPitchOrLinearSize;
			int w = header.dwWidth;
			int h = header.dwHeight;
			for (int i = 0; i < mip_id; i++)
			{
				//GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, IntPtr.Zero + offset);
				offset += temp_size * header.dwDepth;

				w = System.Math.Max(w >> 1, 1);
				h = System.Math.Max(h >> 1, 1);

				temp_size = System.Math.Max(1, (w + 3) / 4) * System.Math.Max(1, (h + 3) / 4) * blockSize;
				//This works only for square textures
				//temp_size = Math.Max(temp_size/4, blocksize);
			}

			byte[] temp_data = new byte[temp_size];
			Buffer.BlockCopy(Data, offset, temp_data, 0, temp_size);

			return temp_data;
        }

		public bool SetMipMapData(int depth, int mip_id, byte[] data)
		{
			//Calculate mipmap_size
			int offset = 0;
			int temp_size = header.dwPitchOrLinearSize;
			int w = header.dwWidth;
			int h = header.dwHeight;
			for (int i = 0; i < mip_id; i++)
			{
				//GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, IntPtr.Zero + offset);
				offset += temp_size * header.dwDepth;

				w = System.Math.Max(w >> 1, 1);
				h = System.Math.Max(h >> 1, 1);

				temp_size = System.Math.Max(1, (w + 3) / 4) * System.Math.Max(1, (h + 3) / 4) * blockSize;
				//This works only for square textures
				//temp_size = Math.Max(temp_size/4, blocksize);
			}

			//Check input data integrity
			if (data.Length != temp_size)
				return false;

			Buffer.BlockCopy(data, 0, Data, offset, temp_size);
			return true;


		}

	}

	[Flags]
	public enum DWFLAGS
    {
		DDSD_CAPS = 0x1,
		DDSD_HEIGHT = 0x2,
		DDSD_WIDTH = 0x4,
		DDSD_PITCH = 0x8,
		DDSD_PIXELFORMAT = 0x1000,
		DDSD_MIPMAPCOUNT = 0x20000,
		DDSD_LINEARSIZE = 0x80000,
		DDSD_DEPTH = 0x800000
	}

	[Flags]
	public enum DDSCAPS
    {
		DDSCAPS_COMPLEX = 0x8,
		DDSCAPS_TEXTURE = 0x1000,
		DDSCAPS_MIPMAP = 0x400000
    }

	[Flags]
	public enum DDSCAPS2
	{
		DDSCAPS2_CUBEMAP = 0x200,
		DDSCAPS2_CUBEMAP_POSITIVEX = 0x400,
		DDSCAPS2_CUBEMAP_NEGATIVEX = 0x800,
		DDSCAPS2_CUBEMAP_POSITIVEY = 0x1000,
		DDSCAPS2_CUBEMAP_NEGATIVEY = 0x2000,
		DDSCAPS2_CUBEMAP_POSITIVEZ = 0x4000,
		DDSCAPS2_CUBEMAP_NEGATIVEZ = 0x8000,
		DDSCAPS2_VOLUME = 0x200000
	}

	public class DDS_HEADER {
		public int dwSize;
		public DWFLAGS dwFlags;
		public int dwHeight;
		public int dwWidth;
		public int dwPitchOrLinearSize;
		public int dwDepth;
		public int dwMipMapCount;
		public int[] dwReserved1 = new int[11];
		public DDS_PIXELFORMAT ddspf = new DDS_PIXELFORMAT();
		public DDSCAPS dwCaps;
		public DDSCAPS2 dwCaps2;
		public int dwCaps3;
		public int dwCaps4;
		public int dwReserved2;
	}

	public class DDS_HEADER_DXT10 {
		public DXGI_FORMAT dxgiFormat;
		public D3D10_RESOURCE_DIMENSION resourceDimension;
		public uint miscFlag;
		public uint arraySize;
		public uint miscFlags2;
	}

	public class DDS_PIXELFORMAT {
		public int dwSize;
		public int dwFlags;
		public int dwFourCC;
		public int dwRGBBitCount;
		public int dwRBitMask;
		public int dwGBitMask;
		public int dwBBitMask;
		public int dwABitMask;

		public DDS_PIXELFORMAT() {
		}
	}

	public enum DXGI_FORMAT:uint {
		DXGI_FORMAT_UNKNOWN = 0,
		DXGI_FORMAT_R32G32B32A32_TYPELESS = 1,
		DXGI_FORMAT_R32G32B32A32_FLOAT = 2,
		DXGI_FORMAT_R32G32B32A32_UINT = 3,
		DXGI_FORMAT_R32G32B32A32_SINT = 4,
		DXGI_FORMAT_R32G32B32_TYPELESS = 5,
		DXGI_FORMAT_R32G32B32_FLOAT = 6,
		DXGI_FORMAT_R32G32B32_UINT = 7,
		DXGI_FORMAT_R32G32B32_SINT = 8,
		DXGI_FORMAT_R16G16B16A16_TYPELESS = 9,
		DXGI_FORMAT_R16G16B16A16_FLOAT = 10,
		DXGI_FORMAT_R16G16B16A16_UNORM = 11,
		DXGI_FORMAT_R16G16B16A16_UINT = 12,
		DXGI_FORMAT_R16G16B16A16_SNORM = 13,
		DXGI_FORMAT_R16G16B16A16_SINT = 14,
		DXGI_FORMAT_R32G32_TYPELESS = 15,
		DXGI_FORMAT_R32G32_FLOAT = 16,
		DXGI_FORMAT_R32G32_UINT = 17,
		DXGI_FORMAT_R32G32_SINT = 18,
		DXGI_FORMAT_R32G8X24_TYPELESS = 19,
		DXGI_FORMAT_D32_FLOAT_S8X24_UINT = 20,
		DXGI_FORMAT_R32_FLOAT_X8X24_TYPELESS = 21,
		DXGI_FORMAT_X32_TYPELESS_G8X24_UINT = 22,
		DXGI_FORMAT_R10G10B10A2_TYPELESS = 23,
		DXGI_FORMAT_R10G10B10A2_UNORM = 24,
		DXGI_FORMAT_R10G10B10A2_UINT = 25,
		DXGI_FORMAT_R11G11B10_FLOAT = 26,
		DXGI_FORMAT_R8G8B8A8_TYPELESS = 27,
		DXGI_FORMAT_R8G8B8A8_UNORM = 28,
		DXGI_FORMAT_R8G8B8A8_UNORM_SRGB = 29,
		DXGI_FORMAT_R8G8B8A8_UINT = 30,
		DXGI_FORMAT_R8G8B8A8_SNORM = 31,
		DXGI_FORMAT_R8G8B8A8_SINT = 32,
		DXGI_FORMAT_R16G16_TYPELESS = 33,
		DXGI_FORMAT_R16G16_FLOAT = 34,
		DXGI_FORMAT_R16G16_UNORM = 35,
		DXGI_FORMAT_R16G16_UINT = 36,
		DXGI_FORMAT_R16G16_SNORM = 37,
		DXGI_FORMAT_R16G16_SINT = 38,
		DXGI_FORMAT_R32_TYPELESS = 39,
		DXGI_FORMAT_D32_FLOAT = 40,
		DXGI_FORMAT_R32_FLOAT = 41,
		DXGI_FORMAT_R32_UINT = 42,
		DXGI_FORMAT_R32_SINT = 43,
		DXGI_FORMAT_R24G8_TYPELESS = 44,
		DXGI_FORMAT_D24_UNORM_S8_UINT = 45,
		DXGI_FORMAT_R24_UNORM_X8_TYPELESS = 46,
		DXGI_FORMAT_X24_TYPELESS_G8_UINT = 47,
		DXGI_FORMAT_R8G8_TYPELESS = 48,
		DXGI_FORMAT_R8G8_UNORM = 49,
		DXGI_FORMAT_R8G8_UINT = 50,
		DXGI_FORMAT_R8G8_SNORM = 51,
		DXGI_FORMAT_R8G8_SINT = 52,
		DXGI_FORMAT_R16_TYPELESS = 53,
		DXGI_FORMAT_R16_FLOAT = 54,
		DXGI_FORMAT_D16_UNORM = 55,
		DXGI_FORMAT_R16_UNORM = 56,
		DXGI_FORMAT_R16_UINT = 57,
		DXGI_FORMAT_R16_SNORM = 58,
		DXGI_FORMAT_R16_SINT = 59,
		DXGI_FORMAT_R8_TYPELESS = 60,
		DXGI_FORMAT_R8_UNORM = 61,
		DXGI_FORMAT_R8_UINT = 62,
		DXGI_FORMAT_R8_SNORM = 63,
		DXGI_FORMAT_R8_SINT = 64,
		DXGI_FORMAT_A8_UNORM = 65,
		DXGI_FORMAT_R1_UNORM = 66,
		DXGI_FORMAT_R9G9B9E5_SHAREDEXP = 67,
		DXGI_FORMAT_R8G8_B8G8_UNORM = 68,
		DXGI_FORMAT_G8R8_G8B8_UNORM = 69,
		DXGI_FORMAT_BC1_TYPELESS = 70,
		DXGI_FORMAT_BC1_UNORM = 71,
		DXGI_FORMAT_BC1_UNORM_SRGB = 72,
		DXGI_FORMAT_BC2_TYPELESS = 73,
		DXGI_FORMAT_BC2_UNORM = 74,
		DXGI_FORMAT_BC2_UNORM_SRGB = 75,
		DXGI_FORMAT_BC3_TYPELESS = 76,
		DXGI_FORMAT_BC3_UNORM = 77,
		DXGI_FORMAT_BC3_UNORM_SRGB = 78,
		DXGI_FORMAT_BC4_TYPELESS = 79,
		DXGI_FORMAT_BC4_UNORM = 80,
		DXGI_FORMAT_BC4_SNORM = 81,
		DXGI_FORMAT_BC5_TYPELESS = 82,
		DXGI_FORMAT_BC5_UNORM = 83,
		DXGI_FORMAT_BC5_SNORM = 84,
		DXGI_FORMAT_B5G6R5_UNORM = 85,
		DXGI_FORMAT_B5G5R5A1_UNORM = 86,
		DXGI_FORMAT_B8G8R8A8_UNORM = 87,
		DXGI_FORMAT_B8G8R8X8_UNORM = 88,
		DXGI_FORMAT_R10G10B10_XR_BIAS_A2_UNORM = 89,
		DXGI_FORMAT_B8G8R8A8_TYPELESS = 90,
		DXGI_FORMAT_B8G8R8A8_UNORM_SRGB = 91,
		DXGI_FORMAT_B8G8R8X8_TYPELESS = 92,
		DXGI_FORMAT_B8G8R8X8_UNORM_SRGB = 93,
		DXGI_FORMAT_BC6H_TYPELESS = 94,
		DXGI_FORMAT_BC6H_UF16 = 95,
		DXGI_FORMAT_BC6H_SF16 = 96,
		DXGI_FORMAT_BC7_TYPELESS = 97,
		DXGI_FORMAT_BC7_UNORM = 98,
		DXGI_FORMAT_BC7_UNORM_SRGB = 99,
		DXGI_FORMAT_AYUV = 100,
		DXGI_FORMAT_Y410 = 101,
		DXGI_FORMAT_Y416 = 102,
		DXGI_FORMAT_NV12 = 103,
		DXGI_FORMAT_P010 = 104,
		DXGI_FORMAT_P016 = 105,
		DXGI_FORMAT_420_OPAQUE = 106,
		DXGI_FORMAT_YUY2 = 107,
		DXGI_FORMAT_Y210 = 108,
		DXGI_FORMAT_Y216 = 109,
		DXGI_FORMAT_NV11 = 110,
		DXGI_FORMAT_AI44 = 111,
		DXGI_FORMAT_IA44 = 112,
		DXGI_FORMAT_P8 = 113,
		DXGI_FORMAT_A8P8 = 114,
		DXGI_FORMAT_B4G4R4A4_UNORM = 115,
		DXGI_FORMAT_FORCE_UINT = 0xffffffff
	}

	public enum D3D10_RESOURCE_DIMENSION {
		D3D10_RESOURCE_DIMENSION_UNKNOWN = 0,
		D3D10_RESOURCE_DIMENSION_BUFFER = 1,
		D3D10_RESOURCE_DIMENSION_TEXTURE1D = 2,
		D3D10_RESOURCE_DIMENSION_TEXTURE2D = 3,
		D3D10_RESOURCE_DIMENSION_TEXTURE3D = 4
	}
}
