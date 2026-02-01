# Security Policy

## Supported Versions

The following versions of CSharp Compound Docs are currently supported with security updates:

| Version | Supported          |
| ------- | ------------------ |
| 0.1.x   | :white_check_mark: |

As the project is in preview, we recommend always using the latest version.

---

## Reporting a Vulnerability

We take security vulnerabilities seriously. If you discover a security issue, please report it responsibly.

### How to Report

**DO NOT** create a public GitHub issue for security vulnerabilities.

Instead, please report security vulnerabilities by emailing:

```
security@example.com
```

Or use GitHub's private vulnerability reporting feature:
1. Go to the repository's Security tab
2. Click "Report a vulnerability"
3. Fill out the form with details

### What to Include

When reporting a vulnerability, please include:

1. **Description**: Clear description of the vulnerability
2. **Impact**: What an attacker could accomplish
3. **Steps to Reproduce**: Detailed steps to reproduce the issue
4. **Affected Versions**: Which versions are affected
5. **Suggested Fix**: If you have one (optional)

### Response Timeline

| Action | Timeline |
|--------|----------|
| Initial response | Within 48 hours |
| Vulnerability confirmation | Within 7 days |
| Fix development | Depends on severity |
| Public disclosure | After fix is available |

### Severity Levels

| Level | Description | Response Time |
|-------|-------------|---------------|
| Critical | Remote code execution, data breach | Within 24 hours |
| High | Privilege escalation, significant data exposure | Within 7 days |
| Medium | Limited data exposure, service disruption | Within 30 days |
| Low | Minor issues, hardening improvements | Next release |

---

## Security Update Process

### Notification

When a security fix is released:

1. **Security advisory** published on GitHub
2. **CHANGELOG.md** updated with security notes
3. **Release notes** include security fix details
4. **Email notification** to subscribed users (if applicable)

### Applying Updates

```bash
# Check for vulnerable packages
dotnet list package --vulnerable

# Update to latest version
git pull origin master
dotnet restore
dotnet build
```

---

## Security Considerations

### Data Security

#### Document Storage

- Documents stored on local file system
- File permissions should be restricted to user
- Sensitive data in documents is the user's responsibility

#### Database Security

- PostgreSQL binds to localhost only (127.0.0.1)
- Default credentials are for local development only
- Production deployments should use strong, unique passwords

#### Network Security

- MCP server uses stdio transport (no network exposure)
- Ollama binds to localhost only
- No external network connections by default

### Authentication and Authorization

#### Current State

- No built-in authentication (single-user, local deployment)
- Relies on file system permissions
- Database access via localhost only

#### Recommendations

For shared or production environments:

1. Change default database credentials
2. Enable PostgreSQL SSL connections
3. Use Docker secrets for credentials
4. Restrict file system permissions

### Secrets Management

#### Environment Variables

```bash
# Recommended for credentials
export COMPOUNDING_POSTGRES_PASSWORD=your-secure-password
```

#### Configuration Files

- Never commit credentials to version control
- Use `.gitignore` for local config overrides
- Consider using a secrets manager for production

### Docker Security

#### Container Isolation

```yaml
# docker-compose.yml security settings
services:
  postgres:
    # Bind to localhost only
    ports:
      - "127.0.0.1:5433:5432"
    # Run as non-root (if image supports)
    user: "999:999"
```

#### Image Verification

- Use official images from trusted registries
- Pin image versions for reproducibility
- Regularly update base images

### Dependency Security

#### Vulnerability Scanning

```bash
# Check for vulnerable NuGet packages
dotnet list package --vulnerable

# Check for outdated packages
dotnet list package --outdated
```

#### Supply Chain Security

- All dependencies from official NuGet registry
- No vendored or self-hosted packages
- Regular dependency audits

---

## Known Security Considerations

### Local-Only Design

This plugin is designed for local, single-user deployment:

| Consideration | Status | Notes |
|---------------|--------|-------|
| Multi-user access | Not supported | Single user per installation |
| Network exposure | Localhost only | Not designed for remote access |
| Authentication | None | Relies on OS-level access control |
| Encryption at rest | Not implemented | Use disk encryption if needed |
| Encryption in transit | N/A | Localhost communication |

### Ollama Integration

- Ollama processes run locally
- Model files stored on local disk
- No API authentication by default

### File Watcher

- Watches only configured directories
- No path traversal protection beyond OS limits
- Ensure config restricts watched paths

---

## Security Best Practices

### For Users

1. **Keep software updated**
   - Regularly update to latest version
   - Monitor security advisories

2. **Secure your environment**
   - Use strong database passwords
   - Restrict file permissions
   - Enable disk encryption

3. **Review document content**
   - Don't store secrets in documents
   - Be cautious with sensitive information

4. **Monitor access**
   - Review Docker container logs
   - Monitor database access patterns

### For Contributors

1. **Secure coding**
   - Validate all inputs
   - Use parameterized queries
   - Avoid hardcoded secrets

2. **Dependency management**
   - Minimize dependencies
   - Use well-maintained packages
   - Keep dependencies updated

3. **Testing**
   - Include security-relevant test cases
   - Test error handling paths
   - Validate input sanitization

---

## Compliance

### Data Privacy

- All data stored locally
- No telemetry or analytics
- No external data transmission
- User controls all data

### Licensing

- MIT License
- Open source dependencies
- No proprietary components

---

## Contact

For security-related questions or concerns:

- **Security issues**: security@example.com
- **General questions**: GitHub Discussions
- **Bug reports**: GitHub Issues (non-security)

---

## Acknowledgments

We appreciate responsible disclosure from the security research community. Contributors who report valid security vulnerabilities will be acknowledged in release notes (unless they prefer to remain anonymous).
