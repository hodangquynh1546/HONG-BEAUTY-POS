namespace HongBeautyPOS.Application;

public static class ProductImageRules
{
    public static string? Normalize(string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return null;
        if (data.Length > 240_000) throw new ArgumentException("Ảnh sản phẩm quá lớn. Tối đa 160 KB.");
        var prefix = data.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal) ? "data:image/jpeg;base64," :
            data.StartsWith("data:image/png;base64,", StringComparison.Ordinal) ? "data:image/png;base64," :
            data.StartsWith("data:image/webp;base64,", StringComparison.Ordinal) ? "data:image/webp;base64," : null;
        if (prefix is null) throw new ArgumentException("Chỉ chấp nhận ảnh JPEG, PNG hoặc WebP.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(data[prefix.Length..]); }
        catch (FormatException) { throw new ArgumentException("Dữ liệu ảnh không hợp lệ."); }
        if (bytes.Length is 0 or > 160_000) throw new ArgumentException("Ảnh sản phẩm quá lớn. Tối đa 160 KB.");
        var valid = prefix.Contains("jpeg") ? bytes.Length > 2 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff :
            prefix.Contains("png") ? bytes.Length > 7 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) :
            bytes.Length > 11 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        if (!valid) throw new ArgumentException("Định dạng ảnh không hợp lệ.");
        return data;
    }
}
