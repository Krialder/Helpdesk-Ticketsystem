using Microsoft.AspNetCore.Identity;

namespace Ticketsystem.Kern.Domain;

// Identity liefert seine Meldungen englisch, und sie landen ungefiltert in
// der Oberfläche. Übersetzt ist, was hier vorkommen kann; alles andere
// erbt die englische Fassung. Der Anmeldename ist die E-Mail-Adresse,
// deshalb sprechen die Texte von einer Adresse.
public sealed class DeutscheIdentityFehler : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        Fehler(nameof(DefaultError), "Das hat nicht geklappt. Bitte noch einmal versuchen.");

    public override IdentityError PasswordTooShort(int length) =>
        Fehler(nameof(PasswordTooShort), $"Das Passwort braucht mindestens {length} Zeichen.");

    public override IdentityError PasswordRequiresDigit() =>
        Fehler(nameof(PasswordRequiresDigit), "Das Passwort braucht mindestens eine Ziffer.");

    public override IdentityError PasswordRequiresLower() =>
        Fehler(nameof(PasswordRequiresLower), "Das Passwort braucht mindestens einen Kleinbuchstaben.");

    public override IdentityError PasswordRequiresUpper() =>
        Fehler(nameof(PasswordRequiresUpper), "Das Passwort braucht mindestens einen Großbuchstaben.");

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Fehler(nameof(PasswordRequiresNonAlphanumeric),
            "Das Passwort braucht mindestens ein Sonderzeichen, etwa ! oder -.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Fehler(nameof(PasswordRequiresUniqueChars),
            $"Das Passwort braucht mindestens {uniqueChars} verschiedene Zeichen.");

    public override IdentityError PasswordMismatch() =>
        Fehler(nameof(PasswordMismatch), "Das bisherige Passwort stimmt nicht.");

    public override IdentityError DuplicateUserName(string userName) =>
        Fehler(nameof(DuplicateUserName), $"Ein Konto mit der Adresse {userName} gibt es schon.");

    public override IdentityError DuplicateEmail(string email) =>
        Fehler(nameof(DuplicateEmail), $"Ein Konto mit der Adresse {email} gibt es schon.");

    public override IdentityError InvalidEmail(string? email) =>
        Fehler(nameof(InvalidEmail), $"„{email}“ sieht nicht nach einer E-Mail-Adresse aus.");

    public override IdentityError InvalidUserName(string? userName) =>
        Fehler(nameof(InvalidUserName), $"„{userName}“ ist als Anmeldename nicht zulässig.");

    public override IdentityError UserAlreadyHasPassword() =>
        Fehler(nameof(UserAlreadyHasPassword), "Dieses Konto hat bereits ein Passwort.");

    public override IdentityError UserAlreadyInRole(string role) =>
        Fehler(nameof(UserAlreadyInRole), $"Das Konto hat die Rolle {role} bereits.");

    public override IdentityError UserNotInRole(string role) =>
        Fehler(nameof(UserNotInRole), $"Das Konto hat die Rolle {role} nicht.");

    public override IdentityError InvalidToken() =>
        Fehler(nameof(InvalidToken), "Der Vorgang ist abgelaufen. Bitte die Seite neu laden.");

    public override IdentityError ConcurrencyFailure() =>
        Fehler(nameof(ConcurrencyFailure),
            "Das Konto wurde zwischenzeitlich geändert. Bitte die Seite neu laden.");

    private static IdentityError Fehler(string code, string beschreibung) =>
        new() { Code = code, Description = beschreibung };
}
