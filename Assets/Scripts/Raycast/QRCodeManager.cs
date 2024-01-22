using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using ZXing;

public class QRCodeManager
{
    public IEnumerator DecodeQR(XRCpuImage image, Action<string> onDecoded)
    {
        // Define the parameters for converting the image
        var conversionParams = new XRCpuImage.ConversionParams
        {
            // Use the entire image
            inputRect = new RectInt(0, 0, image.width, image.height),
            // Reduce the dimensions by half to save processing time
            outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
            // Use RGB24 format for the output
            outputFormat = TextureFormat.RGB24,
        };

        // Start the asynchronous image conversion
        using (var request = image.ConvertAsync(conversionParams))
        {
            // Wait until the conversion is done
            yield return new WaitUntil(() => request.status.IsDone());

            // If the conversion is not ready, stop the coroutine
            if (request.status != XRCpuImage.AsyncConversionStatus.Ready) yield break;

            // Get the converted data
            var rawData = request.GetData<byte>();

            // Create a new Texture2D from the converted data
            Texture2D texture2D = new Texture2D(
                request.conversionParams.outputDimensions.x,
                request.conversionParams.outputDimensions.y,
                request.conversionParams.outputFormat,
                false
            );
            texture2D.LoadRawTextureData(rawData);
            texture2D.Apply();

            // Get the raw texture data
            byte[] barcodeBitmap = texture2D.GetRawTextureData();

            // Create a LuminanceSource with the raw texture data
            LuminanceSource source = new RGBLuminanceSource(barcodeBitmap, texture2D.width, texture2D.height);

            // Create a new BarcodeReader
            var barcodeReader = new BarcodeReader();

            // Decode the barcode from the LuminanceSource
            var result = barcodeReader.Decode(source);

            // Invoke the callback with the decoded text
            onDecoded?.Invoke(result.Text);
        }
    }
}