using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

public class QRCodeManager
{
    public IEnumerator DecodeQR(XRCpuImage image, Action<string> onDecoded)
    {
        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
            outputFormat = TextureFormat.RGB24,
        };

        using (var request = image.ConvertAsync(conversionParams))
        {
            yield return new WaitUntil(() => request.status.IsDone());

            if (request.status != XRCpuImage.AsyncConversionStatus.Ready) yield break;

            var rawData = request.GetData<byte>();
            Texture2D texture2D = new Texture2D(
                request.conversionParams.outputDimensions.x,
                request.conversionParams.outputDimensions.y,
                request.conversionParams.outputFormat,
                false
            );
            texture2D.LoadRawTextureData(rawData);
            texture2D.Apply();

            byte[] barcodeBitmap = texture2D.GetRawTextureData();
            LuminanceSource source = new RGBLuminanceSource(barcodeBitmap, texture2D.width, texture2D.height);
            var barcodeReader = new BarcodeReader();
            var result = barcodeReader.Decode(source);

            onDecoded?.Invoke(result.Text);
        }
    }
}