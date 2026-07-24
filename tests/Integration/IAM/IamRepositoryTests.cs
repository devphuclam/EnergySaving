// Integration test source for IAM PostgreSQL adapters.
// Requires approved Npgsql packages and a configured PostgreSQL connection.
// BLOCKED_BY_PACKAGE_POLICY / BLOCKED_BY_DATABASE_ACCESS until approved packages and endpoint exist.
//
// Test categories when enabled:
// - UserAccountRepositoryTests: CRUD, uniqueness, status transitions
// - ScopeRepositoryTests: assignment, uniqueness, removal
// - CapabilityRepositoryTests: seed data, assignment, revocation
// - SessionRepositoryTests: create, lookup by hash, revoke, revoke-all

namespace IUMP.Tests.Integration.IAM;

public static class IamRepositoryTestSource
{
    // Placeholder for integration test class.
    // Reference: database/migrations/0002_iam_foundation.sql
    // Source is available for review. Not executable without approved packages and database access.
}
