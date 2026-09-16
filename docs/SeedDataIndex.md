# ?? Seed Data Documentation Index

## Quick Navigation

### ?? Getting Started
- [Database Setup Guide](../README-DATABASE.md) - Connection string configuration
- [Main README](../README.md) - Project overview and quick start

### ?? Seed Data
- [Seed Data Overview](../README-SEED-DATA.md) - What data is seeded
- [Seeders Architecture](../README-SEEDERS.md) - How seeders are organized
- [Execution Tree](./SeederExecutionTree.md) - Visual representation of seeding process

---

## ?? Summary

### What is Seeded?

| Entity | Count | Seeder | Order |
|--------|-------|--------|-------|
| Admin Roles | 5 | AdminRolesSeeder | 1 |
| Categories | 14 | CategoriesSeeder | 2 |
| Platform Fees | 5 | PlatformFeesSeeder | 3 |
| Sample Users | 5 | UsersSeeder | 4 |
| Seller Wallets | 3-4 | SellerWalletsSeeder | 5 |
| Sample Products | 8 | ProductsSeeder | 6 |
| Stores | 2-3 | StoresSeeder | 7 |
| Addresses | 5-6 | AddressesSeeder | 8 |

**Total:** ~50 records across 8 entity types

---

## ??? File Structure

```
ebay_clone_adminRole/
??? README.md                          # Main project README
??? README-DATABASE.md                 # Database setup guide
??? README-SEED-DATA.md               # Seed data details
??? README-SEEDERS.md                 # Seeder architecture
??? setup-database.ps1                # Database setup script
??? docs/
?   ??? SeederExecutionTree.md        # Visual seeding flow
??? src/
    ??? Infrastructure/
        ??? Data/
            ??? ApplicationDbContextInitialiser.cs  # Orchestrator
            ??? Seeders/
                ??? ISeeder.cs                 # Interface
                ??? AdminRolesSeeder.cs
                ??? CategoriesSeeder.cs
                ??? PlatformFeesSeeder.cs
                ??? UsersSeeder.cs
                ??? SellerWalletsSeeder.cs
                ??? ProductsSeeder.cs
                ??? StoresSeeder.cs
                ??? AddressesSeeder.cs
```

---

## ?? Key Features

### ? Separation of Concerns
- Each seeder handles ONE entity type
- Easy to find and modify specific data
- Testable independently

### ? Ordered Execution
- Seeders run in defined order (1-8)
- Dependencies automatically resolved
- No race conditions

### ? Idempotent
- Safe to run multiple times
- Checks if data exists before seeding
- Won't create duplicates

### ? Production-Ready Data
- Realistic eBay-like data structure
- Proper relationships and constraints
- Sample data for testing scenarios

---

## ?? Default Credentials

### Admin Dashboard
```
Email: administrator@localhost
Password: Administrator1!
Role: Administrator
```

### Sample Users
```
All sample users have password: Password123!

Users:
- john_buyer (Buyer)
- tech_seller_pro (Seller, 2FA enabled)
- fashion_boutique (Seller)
- pending_seller (Pending approval)
- collectibles_expert (Seller)
```

---

## ?? Quick Commands

### Run Application (Auto-seed on first run)
```powershell
cd src/Web
dotnet run
```

### Reset and Re-seed
```powershell
cd src/Infrastructure
dotnet ef database drop --force --context ApplicationDbContext --startup-project ../Web
cd ../Web
dotnet run
```

### View Seeded Data
```sql
-- Check all seeded tables
SELECT 
    t.name AS TableName,
    SUM(p.rows) AS RowCount
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0,1)
    AND t.name IN ('AdminRoles', 'Category', 'PlatformFees', 'User', 
                   'SellerWallets', 'Product', 'Store', 'Address')
GROUP BY t.name
ORDER BY t.name;
```

---

## ?? Data Relationships

```
Categories ??
            ??? Products ??? OrderItems
            ??? PlatformFees
            
Users ???? Products
       ??? Stores
       ??? Addresses
       ??? SellerWallets
       ??? Orders
       ??? AdminUserRoles

AdminRoles ??? AdminUserRoles
```

---

## ?? Testing Scenarios

With seeded data, you can test:

1. **User Management**
   - Approve pending_seller
   - Ban/unban users
   - Verify 2FA flow with tech_seller_pro

2. **Product Listing**
   - List new products
   - Edit existing products
   - Calculate fees based on category

3. **Auctions**
   - Bid on Air Jordan 1
   - Bid on Pokémon Charizard
   - Test auction end logic

4. **Orders & Checkout**
   - Buy from Tech Pro Electronics store
   - Use john_buyer's addresses
   - Test payment flow

5. **Admin Features**
   - Assign admin roles
   - Manage platform fees
   - Process disputes

---

## ?? Customization

### Add New Sample User
Edit: `src/Infrastructure/Data/Seeders/UsersSeeder.cs`

### Add New Category
Edit: `src/Infrastructure/Data/Seeders/CategoriesSeeder.cs`

### Modify Platform Fees
Edit: `src/Infrastructure/Data/Seeders/PlatformFeesSeeder.cs`

### Add More Products
Edit: `src/Infrastructure/Data/Seeders/ProductsSeeder.cs`

---

## ?? Best Practices

1. **Always test seeders after changes**
   ```powershell
   dotnet test
   ```

2. **Check logs for seeding errors**
   ```powershell
   dotnet run --project src/Web | Select-String "Seed"
   ```

3. **Verify data integrity**
   ```sql
   -- Check foreign key constraints
   SELECT * FROM sys.foreign_keys;
   ```

4. **Document custom data**
   - Update README-SEED-DATA.md if adding new data
   - Update SeederExecutionTree.md if changing order

---

## ?? Troubleshooting

### Seeding Not Running
- Check environment is Development
- Verify database exists
- Check logs: `dotnet run --project src/Web`

### Duplicate Data
- Seeders are idempotent, should not happen
- If occurs, drop and recreate database

### Foreign Key Errors
- Check seeder execution order
- Verify dependent data exists

### Password Validation Errors
- Ensure password meets requirements
- Default: min 6 chars, uppercase, lowercase, number, special char

---

## ?? Additional Resources

- [EF Core Data Seeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding)
- [ASP.NET Core Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [Clean Architecture](https://github.com/jasontaylordev/CleanArchitecture)

---

## ?? Summary

The seed data system provides:
- ? **50+ sample records** across 8 entity types
- ? **Realistic eBay-like data** for testing
- ? **Organized seeder classes** following SOLID principles
- ? **Idempotent execution** safe for production
- ? **Comprehensive documentation** for developers

Perfect for:
- ?? Development and testing
- ?? Learning the domain model
- ?? Training new team members
- ?? Demo presentations
