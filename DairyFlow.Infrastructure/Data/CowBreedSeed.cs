using DairyFlow.Core.Entities;

namespace DairyFlow.Infrastructure.Data;

/// <summary>
/// Static seed data for the CowBreed lookup table.
/// Covers Ethiopian local breeds, exotic dairy breeds, and common crosses.
/// </summary>
public static class CowBreedSeed
{
    public static readonly CowBreed[] All =
    {
        // ── Ethiopian / Local breeds ─────────────────────────────────────────
        new() { Id =  1, Name = "Fogera",           LocalName = "ፎገራ",         Category = "Local",  Origin = "Amhara, Ethiopia",    Notes = "Prized dairy breed from the Lake Tana lowlands; best local milk producer." },
        new() { Id =  2, Name = "Boran",            LocalName = "ቦራና",          Category = "Local",  Origin = "Southern Ethiopia",   Notes = "Hardy zebu breed, dual-purpose; tolerates heat and drought." },
        new() { Id =  3, Name = "Arsi",             LocalName = "አርሲ",          Category = "Local",  Origin = "Arsi Zone, Ethiopia", Notes = "Highland breed adapted to altitude; moderate milk yield." },
        new() { Id =  4, Name = "Horro",            LocalName = "ሆሮ",           Category = "Local",  Origin = "Horro Guduru, Ethiopia", Notes = "Western highlands breed; good body condition score." },
        new() { Id =  5, Name = "Begait",           LocalName = "በጋይት",         Category = "Local",  Origin = "Tigray, Ethiopia",    Notes = "North Ethiopian breed; well adapted to dry conditions." },
        new() { Id =  6, Name = "Raya",             LocalName = "ራያ",           Category = "Local",  Origin = "Tigray/Afar, Ethiopia", Notes = "Transition-zone breed between highland and lowland." },
        new() { Id =  7, Name = "Jem-Jem",          LocalName = "ጀምጀም",         Category = "Local",  Origin = "Jimma, Ethiopia",     Notes = "Forest-edge breed from southwestern Ethiopia." },
        new() { Id =  8, Name = "Bale",             LocalName = "ባሌ",           Category = "Local",  Origin = "Bale Zone, Ethiopia", Notes = "High-altitude breed; resistant to trypanosomiasis." },
        new() { Id =  9, Name = "Ogaden",           LocalName = "ኦጋዴን",         Category = "Local",  Origin = "Somali Region, Ethiopia", Notes = "Lowland Somali zebu; drought-resilient, mainly beef." },
        new() { Id = 10, Name = "Danakil",          LocalName = "ዳናኪል",         Category = "Local",  Origin = "Afar, Ethiopia",      Notes = "Extremely heat-tolerant Afar lowland zebu." },
        new() { Id = 11, Name = "Sheko",            LocalName = "ሸኮ",           Category = "Local",  Origin = "Bench Sheko Zone",    Notes = "Small forest breed; highly trypano-tolerant." },
        new() { Id = 12, Name = "Guraghe",          LocalName = "ጉራጌ",          Category = "Local",  Origin = "Guraghe Zone",        Notes = "Smallholder breed in central highlands." },
        new() { Id = 13, Name = "Wolayta",          LocalName = "ወላይታ",         Category = "Local",  Origin = "Wolayta Zone",        Notes = "Southern highland breed; used for mixed farming." },

        // ── East African / Regional breeds ───────────────────────────────────
        new() { Id = 14, Name = "East African Zebu",    LocalName = "ዘቡ",       Category = "Local",  Origin = "East Africa",         Notes = "Generic term for indigenous East African zebu types." },
        new() { Id = 15, Name = "Ankole-Watusi",        LocalName = null,        Category = "Local",  Origin = "Great Lakes, Africa", Notes = "Long-horned cattle; kept for prestige and milk in Uganda/Rwanda." },

        // ── Exotic / International dairy breeds ──────────────────────────────
        new() { Id = 16, Name = "Holstein Friesian",    LocalName = "ሆልሽታይን",  Category = "Exotic", Origin = "Netherlands",         Notes = "World's highest-producing dairy breed; 25–35 L/day under good management." },
        new() { Id = 17, Name = "Jersey",               LocalName = "ጀርሲ",      Category = "Exotic", Origin = "Jersey Island, UK",   Notes = "High butterfat milk (5–6%); heat-tolerant; popular in tropics." },
        new() { Id = 18, Name = "Guernsey",             LocalName = "ጉዌርንሲ",   Category = "Exotic", Origin = "Guernsey Island, UK", Notes = "Golden-tinted high-beta-carotene milk; 18–25 L/day." },
        new() { Id = 19, Name = "Ayrshire",             LocalName = "ኤርሻይር",   Category = "Exotic", Origin = "Scotland, UK",        Notes = "Rugged dairy breed; adapts well to varied climates." },
        new() { Id = 20, Name = "Brown Swiss",          LocalName = "ቡናማ ስዊስ", Category = "Exotic", Origin = "Switzerland",         Notes = "High protein milk; good feet and legs; long productive life." },
        new() { Id = 21, Name = "Simmental",            LocalName = "ሲሜንታል",   Category = "Exotic", Origin = "Switzerland",         Notes = "Large dual-purpose breed; good milk and beef traits." },
        new() { Id = 22, Name = "Montbéliarde",        LocalName = null,        Category = "Exotic", Origin = "France",              Notes = "French dual-purpose with excellent health and fertility." },
        new() { Id = 23, Name = "Normande",             LocalName = null,        Category = "Exotic", Origin = "Normandy, France",    Notes = "Triple-purpose dairy/beef/draft; high milk protein." },
        new() { Id = 24, Name = "Milking Shorthorn",    LocalName = null,        Category = "Exotic", Origin = "England",             Notes = "Hardy breed; suits pasture-based systems." },
        new() { Id = 25, Name = "Sahiwal",              LocalName = "ሳሂዋል",    Category = "Exotic", Origin = "Pakistan/India",      Notes = "Best tropical dairy zebu; heat and tick resistant; 8–15 L/day." },
        new() { Id = 26, Name = "Red Sindhi",           LocalName = null,        Category = "Exotic", Origin = "Pakistan",            Notes = "Zebu dairy breed; tolerates harsh conditions; 6–12 L/day." },
        new() { Id = 27, Name = "Gir",                  LocalName = "ጊር",        Category = "Exotic", Origin = "India",               Notes = "Humped Indian dairy zebu; high A2 milk; tick resistant." },

        // ── Crossbreeds ───────────────────────────────────────────────────────
        new() { Id = 28, Name = "HF × Boran Cross",     LocalName = "ሆልሽታይን × ቦራና ድቅልቅ",  Category = "Cross", Origin = "Ethiopia", Notes = "Most common Ethiopian dairy cross; balances high yield with local hardiness." },
        new() { Id = 29, Name = "HF × Fogera Cross",    LocalName = "ሆልሽታይን × ፎገራ ድቅልቅ",  Category = "Cross", Origin = "Ethiopia", Notes = "Popular in Amhara region; improved milk yield with disease tolerance." },
        new() { Id = 30, Name = "Jersey × Zebu Cross",  LocalName = "ጀርሲ × ዘቡ ድቅልቅ",       Category = "Cross", Origin = "Ethiopia", Notes = "Heat-tolerant cross with high-fat milk; suits smallholders." },
        new() { Id = 31, Name = "Sahiwal × HF Cross",   LocalName = "ሳሂዋል × ሆልሽታይን ድቅልቅ", Category = "Cross", Origin = "East Africa", Notes = "Tick-resistant with good milk yield; thrives in semi-arid zones." },
        new() { Id = 32, Name = "Brown Swiss × Zebu",   LocalName = null,                     Category = "Cross", Origin = "Ethiopia", Notes = "Docile temperament; suitable for highland smallholder systems." },

        // ── Other / Unknown ───────────────────────────────────────────────────
        new() { Id = 33, Name = "Other / Mixed",        LocalName = "ሌላ / ድቅልቅ",            Category = "Other", Origin = null, Notes = "Use when exact breed is unknown or not listed." },
    };
}
