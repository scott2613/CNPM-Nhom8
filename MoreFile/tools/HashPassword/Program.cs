using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
Console.Write("Mật khẩu quản trị (ít nhất 4 ký tự, không hiển thị): ");
var password="";ConsoleKeyInfo key;
while((key=Console.ReadKey(true)).Key!=ConsoleKey.Enter){if(key.Key==ConsoleKey.Backspace){if(password.Length>0)password=password[..^1];}else if(!char.IsControl(key.KeyChar))password+=key.KeyChar;}
Console.WriteLine();if(password.Length<4 || password.Length>1024 || string.IsNullOrWhiteSpace(password))throw new InvalidOperationException("Mật khẩu cần từ 4 đến 1024 ký tự và có ít nhất một ký tự khác khoảng trắng.");
Console.WriteLine("Security__AdminPasswordHash="+new PasswordHasher<string>().HashPassword("admin",password));
Console.WriteLine("Security__AdminApiKey="+Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
