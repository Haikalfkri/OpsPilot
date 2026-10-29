using OpsPilot.Domain.Enums;

namespace OpsPilot.Application.Teams;

// data ringkas anggota tim, untuk dropdown (misalnya memilih assignee incident).
// sengaja tanpa email dan tanpa status: semua anggota tim boleh melihat daftar ini
public sealed record TeamMemberDto(Guid Id, string DisplayName, UserRole Role);