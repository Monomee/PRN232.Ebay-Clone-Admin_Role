# ?? Seeder Execution Tree

```
Application Startup (Program.cs)
?
??? InitialiseDatabaseAsync()
    ?
    ??? InitialiseAsync()
    ?   ??? Database.MigrateAsync()
    ?       ??? Apply all pending migrations
    ?
    ??? SeedAsync()
        ?
        ??? SeedIdentityRolesAsync()
        ?   ??? Create "Administrator" role
        ?
        ??? SeedDefaultAdminAsync()
        ?   ??? Create administrator@localhost
        ?
        ??? ExecuteSeedersAsync()
            ?
            ??? [Order: 1] AdminRolesSeeder
            ?   ?? SuperAdmin
            ?   ?? Monitor
            ?   ?? Support
            ?   ?? ContentModerator
            ?   ?? FinanceManager
            ?
            ??? [Order: 2] CategoriesSeeder
            ?   ?? Electronics
            ?   ?? Fashion
            ?   ?? Home & Garden
            ?   ?? Motors
            ?   ?? Collectibles & Art
            ?   ?? Sports & Outdoors
            ?   ?? Toys & Hobbies
            ?   ?? Books, Movies & Music
            ?   ?? Health & Beauty
            ?   ?? Business & Industrial
            ?   ?? Jewelry & Watches
            ?   ?? Baby Essentials
            ?   ?? Pet Supplies
            ?   ?? Musical Instruments
            ?
            ??? [Order: 3] PlatformFeesSeeder
            ?   ?? FinalValueFee (12.9%)
            ?   ?? ListingFee ($0.35)
            ?   ?? Motors Fee (3%, cap $125)
            ?   ?? Collectibles Fee (15%)
            ?   ?? Promoted Listing Fee (8%)
            ?
            ??? [Order: 4] UsersSeeder
            ?   ?? john_buyer (Buyer)
            ?   ?? tech_seller_pro (Seller, 2FA)
            ?   ?? fashion_boutique (Seller)
            ?   ?? pending_seller (Pending)
            ?   ?? collectibles_expert (Seller)
            ?
            ??? [Order: 5] SellerWalletsSeeder
            ?   ?? Wallet for tech_seller_pro
            ?   ?? Wallet for fashion_boutique
            ?   ?? Wallet for pending_seller
            ?   ?? Wallet for collectibles_expert
            ?
            ??? [Order: 6] ProductsSeeder
            ?   ?? iPhone 15 Pro Max
            ?   ?? Sony Headphones
            ?   ?? MacBook Pro
            ?   ?? Air Jordan 1 (Auction)
            ?   ?? Gucci Bag
            ?   ?? Rolex Submariner
            ?   ?? Pokémon Charizard (Auction)
            ?   ?? MJ Signed Basketball
            ?
            ??? [Order: 7] StoresSeeder
            ?   ?? Tech Pro Electronics
            ?   ?? Fashion Boutique Elite
            ?   ?? Rare Collectibles & Memorabilia
            ?
            ??? [Order: 8] AddressesSeeder
                ?? John's Home Address (default)
                ?? John's Office Address
                ?? Tech Seller Business Address
                ?? Fashion Store Address (default)
                ?? Fashion Warehouse Address
```

---

## ?? Data Dependencies Graph

```
                    ???????????????????
                    ?  Categories     ? (Independent)
                    ???????????????????
                             ?
                             ?????????????????
                             ?               ?
                    ???????????????????  ????????????????
                    ? PlatformFees    ?  ?   Products   ?
                    ???????????????????  ????????????????
                                                ?
                                                ?
         ???????????????????????????????????????????????????
         ?                                      ?          ?
         ?                                      ?          ?
???????????????????                    ????????????????  ?
?   Users         ? (Independent)      ?   Stores     ?  ?
???????????????????                    ????????????????  ?
         ?                                                ?
         ???????????????????????????????????????        ?
         ?            ?          ?             ?        ?
????????????????  ??????????  ??????????  ??????????  ?
?SellerWallets?  ?Stores  ?  ?Address ?  ?Products????
????????????????  ??????????  ??????????  ??????????


Legend:
  ? Direct dependency
  ? Creates/References
```

---

## ?? Seeder Idempotency Pattern

All seeders follow this pattern:

```csharp
public async Task SeedAsync()
{
    // 1. Check if data already exists
    if (_context.Entity.Any())
    {
        _logger.LogInformation("Entity already seeded, skipping...");
        return; // ? Exit early, safe to run multiple times
    }

    // 2. Create new data
    var entities = new List<Entity> { /* data */ };

    // 3. Add to context
    _context.Entity.AddRange(entities);

    // 4. Save changes
    await _context.SaveChangesAsync();

    // 5. Log success
    _logger.LogInformation("Seeded {Count} entities", entities.Count);
}
```

**Benefits:**
- ? Can run multiple times without errors
- ? Won't create duplicate data
- ? Logs skip message if already seeded
- ? Safe for production deployments

---

## ?? Execution Time (Approximate)

| Seeder | Time | Records |
|--------|------|---------|
| AdminRolesSeeder | ~50ms | 5 |
| CategoriesSeeder | ~100ms | 14 |
| PlatformFeesSeeder | ~80ms | 5 |
| UsersSeeder | ~200ms | 5 |
| SellerWalletsSeeder | ~150ms | 3-4 |
| ProductsSeeder | ~250ms | 8 |
| StoresSeeder | ~100ms | 2-3 |
| AddressesSeeder | ~150ms | 5-6 |
| **Total** | **~1-1.5s** | **~50 records** |

*Times are approximate and depend on database performance*

---

## ?? Adding New Seeders

When adding a new seeder, consider:

1. **Order:** Choose a number that respects dependencies
2. **Idempotency:** Always check if data exists first
3. **Dependencies:** Document what other seeders it depends on
4. **Logging:** Log both skip and success cases
5. **Testing:** Write unit tests for the seeder

Example order calculation:
- Independent entities: 1-9
- Entities with 1 dependency: 10-19
- Entities with 2+ dependencies: 20+

---

## ?? Sample Output Log

```
info: Starting database seeding...
info: Seeded Identity Role: Administrator
info: Seeded Default Admin User: administrator@localhost
info: Executing seeder: AdminRolesSeeder
info: Seeded 5 Admin Roles
info: Executing seeder: CategoriesSeeder
info: Seeded 14 Categories
info: Executing seeder: PlatformFeesSeeder
info: Seeded 5 Platform Fees
info: Executing seeder: UsersSeeder
info: Seeded 5 Sample Users
info: Executing seeder: SellerWalletsSeeder
info: Seeded 4 Seller Wallets
info: Executing seeder: ProductsSeeder
info: Seeded 8 Sample Products
info: Executing seeder: StoresSeeder
info: Seeded 3 Stores
info: Executing seeder: AddressesSeeder
info: Seeded 6 Addresses
info: Database seeding completed successfully
```
