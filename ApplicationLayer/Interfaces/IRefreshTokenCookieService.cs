namespace Application.Interfaces
{
   
    public interface IRefreshTokenCookieService
    {
        
        void SetRefreshToken(string token);

        
        string? GetRefreshToken();

       
        void RemoveRefreshToken();
    }
}
