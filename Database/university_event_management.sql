-- ============================================================
-- UNIVERSITY EVENT MANAGEMENT SYSTEM
-- COMPLETE MYSQL 8 DATABASE
-- 35 RELATIONAL TABLES FOR CAMPUS EVENT MANAGEMENT
-- ============================================================

DROP DATABASE IF EXISTS university_event_management;

CREATE DATABASE university_event_management
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE university_event_management;

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;


-- ============================================================
-- 01. ROLES
-- ============================================================

CREATE TABLE roles (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name VARCHAR(100) NOT NULL,
    description VARCHAR(500) NULL,
    is_system_role BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_roles_name (name)
) ENGINE=InnoDB;


-- ============================================================
-- 02. PERMISSIONS
-- ============================================================

CREATE TABLE permissions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    name VARCHAR(150) NOT NULL,
    description VARCHAR(500) NULL,
    module VARCHAR(100) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_permissions_name (name),
    KEY idx_permissions_module (module)
) ENGINE=InnoDB;


-- ============================================================
-- 03. FACULTIES
-- ============================================================

CREATE TABLE faculties (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NULL,
    description TEXT NULL,

    dean_name VARCHAR(200) NULL,
    email VARCHAR(255) NULL,
    phone VARCHAR(50) NULL,

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_faculties_name (name),
    UNIQUE KEY uq_faculties_code (code)
) ENGINE=InnoDB;


-- ============================================================
-- 04. DEPARTMENTS
-- ============================================================

CREATE TABLE departments (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    faculty_id BIGINT UNSIGNED NOT NULL,

    name VARCHAR(200) NOT NULL,
    code VARCHAR(50) NULL,
    description TEXT NULL,

    head_name VARCHAR(200) NULL,
    email VARCHAR(255) NULL,
    phone VARCHAR(50) NULL,

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_departments_faculty_name (faculty_id, name),
    UNIQUE KEY uq_departments_code (code),

    KEY idx_departments_faculty (faculty_id),

    CONSTRAINT fk_departments_faculty
        FOREIGN KEY (faculty_id)
        REFERENCES faculties(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 05. USERS
-- ============================================================

CREATE TABLE users (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    department_id BIGINT UNSIGNED NULL,

    username VARCHAR(100) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,

    first_name VARCHAR(100) NOT NULL,
    middle_name VARCHAR(100) NULL,
    last_name VARCHAR(100) NOT NULL,

    student_id VARCHAR(100) NULL,
    employee_id VARCHAR(100) NULL,

    phone VARCHAR(50) NULL,
    profile_image_url VARCHAR(1000) NULL,
    bio TEXT NULL,

    account_type ENUM(
        'STUDENT',
        'STAFF',
        'FACULTY',
        'ORGANIZATION',
        'EXTERNAL',
        'ADMIN',
        'SUPERADMIN'
    ) NOT NULL DEFAULT 'STUDENT',

    account_status ENUM(
        'PENDING',
        'ACTIVE',
        'SUSPENDED',
        'LOCKED',
        'INACTIVE'
    ) NOT NULL DEFAULT 'PENDING',

    email_verified BOOLEAN NOT NULL DEFAULT FALSE,
    phone_verified BOOLEAN NOT NULL DEFAULT FALSE,

    last_login_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_users_username (username),
    UNIQUE KEY uq_users_email (email),
    UNIQUE KEY uq_users_student_id (student_id),
    UNIQUE KEY uq_users_employee_id (employee_id),

    KEY idx_users_department (department_id),
    KEY idx_users_status (account_status),
    KEY idx_users_account_type (account_type),

    CONSTRAINT fk_users_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 06. ROLE_PERMISSIONS
-- ============================================================

CREATE TABLE role_permissions (
    role_id BIGINT UNSIGNED NOT NULL,
    permission_id BIGINT UNSIGNED NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (role_id, permission_id),

    CONSTRAINT fk_role_permissions_role
        FOREIGN KEY (role_id)
        REFERENCES roles(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_role_permissions_permission
        FOREIGN KEY (permission_id)
        REFERENCES permissions(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 07. SESSIONS
-- ============================================================

CREATE TABLE sessions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    session_token_hash VARCHAR(255) NOT NULL,

    ip_address VARCHAR(45) NULL,
    user_agent VARCHAR(500) NULL,
    device_name VARCHAR(255) NULL,

    started_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    last_activity_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    expires_at DATETIME(6) NOT NULL,
    revoked_at DATETIME(6) NULL,

    PRIMARY KEY (id),

    UNIQUE KEY uq_sessions_token (session_token_hash),

    KEY idx_sessions_user (user_id),
    KEY idx_sessions_expires (expires_at),

    CONSTRAINT fk_sessions_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 08. AUTH_TOKENS
-- ============================================================

CREATE TABLE auth_tokens (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    token_hash VARCHAR(255) NOT NULL,

    token_type ENUM(
        'PASSWORD_RESET',
        'EMAIL_VERIFICATION'
    ) NOT NULL,

    expires_at DATETIME(6) NOT NULL,
    used_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_auth_tokens_hash (token_hash),

    KEY idx_auth_tokens_user (user_id),
    KEY idx_auth_tokens_type (token_type),
    KEY idx_auth_tokens_expires (expires_at),

    CONSTRAINT fk_auth_tokens_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 09. USER_ROLES
-- ============================================================

CREATE TABLE user_roles (
    user_id BIGINT UNSIGNED NOT NULL,
    role_id BIGINT UNSIGNED NOT NULL,

    assigned_by BIGINT UNSIGNED NULL,

    assigned_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (user_id, role_id),

    KEY idx_user_roles_role (role_id),
    KEY idx_user_roles_assigned_by (assigned_by),

    CONSTRAINT fk_user_roles_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_user_roles_role
        FOREIGN KEY (role_id)
        REFERENCES roles(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_user_roles_assigned_by
        FOREIGN KEY (assigned_by)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 10. ORGANIZATIONS
-- ============================================================

CREATE TABLE organizations (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    department_id BIGINT UNSIGNED NULL,

    name VARCHAR(200) NOT NULL,
    short_name VARCHAR(100) NULL,

    description TEXT NULL,

    organization_type ENUM(
        'CLUB',
        'OFFICE',
        'ASSOCIATION',
        'STUDENT_UNION',
        'DEPARTMENT',
        'FACULTY',
        'OTHER'
    ) NOT NULL DEFAULT 'CLUB',

    email VARCHAR(255) NULL,
    phone VARCHAR(50) NULL,
    logo_url VARCHAR(1000) NULL,

    status ENUM(
        'PENDING',
        'ACTIVE',
        'SUSPENDED',
        'INACTIVE'
    ) NOT NULL DEFAULT 'PENDING',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_organizations_name (name),

    KEY idx_organizations_department (department_id),
    KEY idx_organizations_type (organization_type),

    CONSTRAINT fk_organizations_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 11. ORGANIZATION_MEMBERS
-- ============================================================

CREATE TABLE organization_members (
    organization_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    membership_role ENUM(
        'MEMBER',
        'OFFICER',
        'SECRETARY',
        'TREASURER',
        'PRESIDENT',
        'ADMIN'
    ) NOT NULL DEFAULT 'MEMBER',

    joined_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    left_at DATETIME(6) NULL,

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    PRIMARY KEY (organization_id, user_id),

    KEY idx_org_members_user (user_id),

    CONSTRAINT fk_org_members_organization
        FOREIGN KEY (organization_id)
        REFERENCES organizations(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_org_members_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 12. CLASS_SCHEDULES
-- ============================================================

CREATE TABLE class_schedules (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    department_id BIGINT UNSIGNED NOT NULL,

    course_code VARCHAR(50) NOT NULL,
    course_name VARCHAR(255) NOT NULL,
    section_name VARCHAR(100) NULL,

    academic_year VARCHAR(50) NULL,
    semester VARCHAR(50) NULL,

    day_of_week ENUM(
        'MONDAY',
        'TUESDAY',
        'WEDNESDAY',
        'THURSDAY',
        'FRIDAY',
        'SATURDAY',
        'SUNDAY'
    ) NOT NULL,

    start_time TIME NOT NULL,
    end_time TIME NOT NULL,

    room_name VARCHAR(200) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_class_department (department_id),
    KEY idx_class_course (course_code),
    KEY idx_class_day_time (
        day_of_week,
        start_time,
        end_time
    ),

    CONSTRAINT fk_class_schedules_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,

    CONSTRAINT chk_class_schedule_time
        CHECK (end_time > start_time)
) ENGINE=InnoDB;


-- ============================================================
-- 13. VENUES
-- ============================================================

CREATE TABLE venues (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    name VARCHAR(200) NOT NULL,

    building_name VARCHAR(200) NULL,
    room_number VARCHAR(100) NULL,

    description TEXT NULL,

    capacity INT UNSIGNED NOT NULL DEFAULT 1,

    venue_type ENUM(
        'CLASSROOM',
        'LECTURE_HALL',
        'AUDITORIUM',
        'LAB',
        'SPORTS_FIELD',
        'MEETING_ROOM',
        'OUTDOOR',
        'OTHER'
    ) NOT NULL DEFAULT 'CLASSROOM',

    latitude DECIMAL(10,7) NULL,
    longitude DECIMAL(10,7) NULL,

    amenities TEXT NULL,

    status ENUM(
        'AVAILABLE',
        'MAINTENANCE',
        'INACTIVE'
    ) NOT NULL DEFAULT 'AVAILABLE',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_venues_name (name),

    KEY idx_venues_status (status),
    KEY idx_venues_type (venue_type)
) ENGINE=InnoDB;


-- ============================================================
-- 14. EVENT_CATEGORIES
-- ============================================================

CREATE TABLE event_categories (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    name VARCHAR(100) NOT NULL,
    slug VARCHAR(120) NOT NULL,

    description VARCHAR(500) NULL,
    icon VARCHAR(100) NULL,

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_event_categories_name (name),
    UNIQUE KEY uq_event_categories_slug (slug)
) ENGINE=InnoDB;


-- ============================================================
-- 15. EVENT_TAGS
-- ============================================================

CREATE TABLE event_tags (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    name VARCHAR(100) NOT NULL,
    slug VARCHAR(120) NOT NULL,

    description VARCHAR(500) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_event_tags_name (name),
    UNIQUE KEY uq_event_tags_slug (slug)
) ENGINE=InnoDB;


-- ============================================================
-- 16. EVENTS
-- ============================================================

CREATE TABLE events (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    title VARCHAR(255) NOT NULL,
    slug VARCHAR(300) NOT NULL,

    description TEXT NULL,
    short_description VARCHAR(500) NULL,

    category_id BIGINT UNSIGNED NOT NULL,
    organizer_id BIGINT UNSIGNED NOT NULL,
    organization_id BIGINT UNSIGNED NULL,
    venue_id BIGINT UNSIGNED NULL,

    start_at DATETIME(6) NOT NULL,
    end_at DATETIME(6) NOT NULL,

    registration_start_at DATETIME(6) NULL,
    registration_end_at DATETIME(6) NULL,

    capacity INT UNSIGNED NULL,

    registration_required BOOLEAN NOT NULL DEFAULT TRUE,
    allow_waitlist BOOLEAN NOT NULL DEFAULT FALSE,

    event_mode ENUM(
        'IN_PERSON',
        'ONLINE',
        'HYBRID'
    ) NOT NULL DEFAULT 'IN_PERSON',

    online_url VARCHAR(1000) NULL,
    image_url VARCHAR(1000) NULL,

    status ENUM(
        'DRAFT',
        'PENDING_APPROVAL',
        'APPROVED',
        'PUBLISHED',
        'REJECTED',
        'CANCELLED',
        'COMPLETED'
    ) NOT NULL DEFAULT 'DRAFT',

    approval_status ENUM(
        'NOT_REQUIRED',
        'PENDING',
        'APPROVED',
        'REJECTED'
    ) NOT NULL DEFAULT 'PENDING',

    approved_by BIGINT UNSIGNED NULL,
    approved_at DATETIME(6) NULL,

    rejection_reason TEXT NULL,

    is_featured BOOLEAN NOT NULL DEFAULT FALSE,
    is_public BOOLEAN NOT NULL DEFAULT TRUE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_events_slug (slug),

    KEY idx_events_category (category_id),
    KEY idx_events_organizer (organizer_id),
    KEY idx_events_organization (organization_id),
    KEY idx_events_venue (venue_id),
    KEY idx_events_start (start_at),
    KEY idx_events_status (status),
    KEY idx_events_approval (approval_status),

    CONSTRAINT fk_events_category
        FOREIGN KEY (category_id)
        REFERENCES event_categories(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,

    CONSTRAINT fk_events_organizer
        FOREIGN KEY (organizer_id)
        REFERENCES users(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,

    CONSTRAINT fk_events_organization
        FOREIGN KEY (organization_id)
        REFERENCES organizations(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_events_venue
        FOREIGN KEY (venue_id)
        REFERENCES venues(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_events_approved_by
        FOREIGN KEY (approved_by)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT chk_events_dates
        CHECK (end_at > start_at)
) ENGINE=InnoDB;


-- ============================================================
-- 17. EVENT_TAG_MAP
-- ============================================================

CREATE TABLE event_tag_map (
    event_id BIGINT UNSIGNED NOT NULL,
    tag_id BIGINT UNSIGNED NOT NULL,

    PRIMARY KEY (event_id, tag_id),

    KEY idx_event_tag_map_tag (tag_id),

    CONSTRAINT fk_event_tag_map_event
        FOREIGN KEY (event_id)
        REFERENCES events(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_event_tag_map_tag
        FOREIGN KEY (tag_id)
        REFERENCES event_tags(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 18. REGISTRATIONS
-- ============================================================

CREATE TABLE registrations (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    event_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    registration_code VARCHAR(100) NOT NULL,
    qr_token VARCHAR(255) NOT NULL,

    status ENUM(
        'REGISTERED',
        'WAITLISTED',
        'CANCELLED',
        'ATTENDED',
        'NO_SHOW'
    ) NOT NULL DEFAULT 'REGISTERED',

    registered_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    cancelled_at DATETIME(6) NULL,
    checked_in_at DATETIME(6) NULL,

    check_in_method ENUM(
        'QR',
        'MANUAL',
        'SYSTEM'
    ) NULL,

    notes VARCHAR(1000) NULL,

    PRIMARY KEY (id),

    UNIQUE KEY uq_registration_event_user (
        event_id,
        user_id
    ),

    UNIQUE KEY uq_registration_code (registration_code),
    UNIQUE KEY uq_registration_qr_token (qr_token),

    KEY idx_registrations_event (event_id),
    KEY idx_registrations_user (user_id),
    KEY idx_registrations_status (status),

    CONSTRAINT fk_registrations_event
        FOREIGN KEY (event_id)
        REFERENCES events(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_registrations_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;





-- ============================================================
-- 23. ANNOUNCEMENTS
-- ============================================================

CREATE TABLE announcements (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    title VARCHAR(255) NOT NULL,
    slug VARCHAR(300) NOT NULL,

    content TEXT NOT NULL,
    summary VARCHAR(500) NULL,

    author_id BIGINT UNSIGNED NULL,
    department_id BIGINT UNSIGNED NULL,

    announcement_type ENUM(
        'NEWS',
        'NOTICE',
        'ALERT',
        'CLOSURE',
        'ACADEMIC',
        'CAREER',
        'GENERAL'
    ) NOT NULL DEFAULT 'GENERAL',

    priority ENUM(
        'LOW',
        'NORMAL',
        'HIGH',
        'URGENT'
    ) NOT NULL DEFAULT 'NORMAL',

    image_url VARCHAR(1000) NULL,

    published_at DATETIME(6) NULL,
    expires_at DATETIME(6) NULL,

    status ENUM(
        'DRAFT',
        'PUBLISHED',
        'ARCHIVED'
    ) NOT NULL DEFAULT 'DRAFT',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_announcements_slug (slug),

    KEY idx_announcements_author (author_id),
    KEY idx_announcements_department (department_id),
    KEY idx_announcements_status (status),

    CONSTRAINT fk_announcements_author
        FOREIGN KEY (author_id)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_announcements_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 24. NOTIFICATIONS
-- ============================================================

CREATE TABLE notifications (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    title VARCHAR(255) NOT NULL,
    message TEXT NOT NULL,

    notification_type ENUM(
        'EVENT',
        'REGISTRATION',
        'REMINDER',
        'ANNOUNCEMENT',
        'SYSTEM',
        'FEEDBACK',
        'POLL'
    ) NOT NULL DEFAULT 'SYSTEM',

    related_entity_type VARCHAR(100) NULL,
    related_entity_id BIGINT UNSIGNED NULL,

    action_url VARCHAR(1000) NULL,

    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at DATETIME(6) NULL,

    expires_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_notifications_user (user_id),
    KEY idx_notifications_unread (user_id, is_read),
    KEY idx_notifications_expires (expires_at),

    CONSTRAINT fk_notifications_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 25. DEVICE_TOKENS
-- ============================================================

CREATE TABLE device_tokens (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    token VARCHAR(1000) NOT NULL,

    platform ENUM(
        'WEB',
        'ANDROID',
        'IOS',
        'DESKTOP',
        'OTHER'
    ) NOT NULL DEFAULT 'WEB',

    device_name VARCHAR(255) NULL,

    is_active BOOLEAN NOT NULL DEFAULT TRUE,

    last_used_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_device_tokens_token (token(255)),

    KEY idx_device_tokens_user (user_id),

    CONSTRAINT fk_device_tokens_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 26. CALENDAR_SYNCS
-- ============================================================

CREATE TABLE calendar_syncs (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    provider ENUM(
        'GOOGLE',
        'APPLE',
        'OUTLOOK'
    ) NOT NULL,

    provider_account_id VARCHAR(255) NULL,

    access_token_encrypted TEXT NULL,
    refresh_token_encrypted TEXT NULL,

    calendar_id VARCHAR(500) NULL,

    sync_enabled BOOLEAN NOT NULL DEFAULT TRUE,

    last_synced_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_calendar_sync_user_provider (
        user_id,
        provider
    ),

    CONSTRAINT fk_calendar_syncs_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 27. USER_PREFERENCES
-- ============================================================

CREATE TABLE user_preferences (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,

    email_notifications BOOLEAN NOT NULL DEFAULT TRUE,
    push_notifications BOOLEAN NOT NULL DEFAULT TRUE,
    sms_notifications BOOLEAN NOT NULL DEFAULT FALSE,

    event_reminders BOOLEAN NOT NULL DEFAULT TRUE,
    announcement_notifications BOOLEAN NOT NULL DEFAULT TRUE,
    career_notifications BOOLEAN NOT NULL DEFAULT TRUE,
    comment_notifications BOOLEAN NOT NULL DEFAULT TRUE,

    reminder_minutes INT UNSIGNED NOT NULL DEFAULT 30,

    preferred_language VARCHAR(20) NOT NULL DEFAULT 'en',
    timezone VARCHAR(100) NOT NULL DEFAULT 'Africa/Addis_Ababa',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_user_preferences_user (user_id),

    CONSTRAINT fk_user_preferences_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 28. USER_DEPT_SUBSCRIPTIONS (Personalization: Department Follows)
-- ============================================================

CREATE TABLE user_dept_subscriptions (
    sub_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id BIGINT UNSIGNED NOT NULL,
    department_id BIGINT UNSIGNED NOT NULL,
    notify_on_new_event TINYINT(1) NOT NULL DEFAULT 1,
    subscribed_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (sub_id),
    UNIQUE KEY uq_user_dept_subscription (user_id, department_id),
    KEY idx_user_dept_sub_department (department_id),
    KEY idx_user_dept_sub_user (user_id),

    CONSTRAINT fk_user_dept_sub_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_user_dept_sub_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 29. USER_CATEGORY_INTERESTS (Personalization: Topic Interests)
-- ============================================================

CREATE TABLE user_category_interests (
    interest_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id BIGINT UNSIGNED NOT NULL,
    category_id BIGINT UNSIGNED NOT NULL,

    interest_level ENUM(
        'LOW',
        'MEDIUM',
        'HIGH'
    ) NOT NULL DEFAULT 'MEDIUM',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (interest_id),
    UNIQUE KEY uq_user_category_interest (user_id, category_id),
    KEY idx_user_category_category (category_id),
    KEY idx_user_category_user (user_id),

    CONSTRAINT fk_user_category_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_user_category_category
        FOREIGN KEY (category_id)
        REFERENCES event_categories(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 30. EVENT_COMMENTS
-- ============================================================

CREATE TABLE event_comments (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    event_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    parent_comment_id BIGINT UNSIGNED NULL,

    comment TEXT NOT NULL,

    is_edited BOOLEAN NOT NULL DEFAULT FALSE,
    edited_at DATETIME(6) NULL,

    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_event_comments_event (event_id),
    KEY idx_event_comments_user (user_id),
    KEY idx_event_comments_parent (parent_comment_id),

    CONSTRAINT fk_event_comments_event
        FOREIGN KEY (event_id)
        REFERENCES events(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_event_comments_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_event_comments_parent
        FOREIGN KEY (parent_comment_id)
        REFERENCES event_comments(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 31. EVENT_FEEDBACK
-- ============================================================

CREATE TABLE event_feedback (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    event_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    rating TINYINT UNSIGNED NOT NULL,

    comment TEXT NULL,

    is_anonymous BOOLEAN NOT NULL DEFAULT FALSE,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_event_feedback_user (
        event_id,
        user_id
    ),

    KEY idx_event_feedback_event (event_id),
    KEY idx_event_feedback_user (user_id),

    CONSTRAINT fk_event_feedback_event
        FOREIGN KEY (event_id)
        REFERENCES events(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_event_feedback_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT chk_event_feedback_rating
        CHECK (rating BETWEEN 1 AND 5)
) ENGINE=InnoDB;





-- ============================================================
-- 34. POLLS
-- ============================================================

CREATE TABLE polls (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    created_by BIGINT UNSIGNED NOT NULL,

    title VARCHAR(255) NOT NULL,
    question TEXT NOT NULL,
    description TEXT NULL,

    start_at DATETIME(6) NULL,
    end_at DATETIME(6) NULL,

    allow_multiple_answers BOOLEAN NOT NULL DEFAULT FALSE,
    anonymous BOOLEAN NOT NULL DEFAULT FALSE,

    status ENUM(
        'DRAFT',
        'ACTIVE',
        'CLOSED',
        'ARCHIVED'
    ) NOT NULL DEFAULT 'DRAFT',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_polls_creator (created_by),
    KEY idx_polls_status (status),

    CONSTRAINT fk_polls_creator
        FOREIGN KEY (created_by)
        REFERENCES users(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE,

    CONSTRAINT chk_polls_dates
        CHECK (
            end_at IS NULL
            OR start_at IS NULL
            OR end_at > start_at
        )
) ENGINE=InnoDB;


-- ============================================================
-- 35. POLL_OPTIONS
-- ============================================================

CREATE TABLE poll_options (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    poll_id BIGINT UNSIGNED NOT NULL,

    option_text VARCHAR(500) NOT NULL,

    display_order INT UNSIGNED NOT NULL DEFAULT 0,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_poll_options_poll (poll_id),
    KEY idx_poll_options_order (poll_id, display_order),

    CONSTRAINT fk_poll_options_poll
        FOREIGN KEY (poll_id)
        REFERENCES polls(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 36. POLL_RESPONSES
-- ============================================================

CREATE TABLE poll_responses (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    poll_id BIGINT UNSIGNED NOT NULL,
    option_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    responded_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    UNIQUE KEY uq_poll_response (
        poll_id,
        option_id,
        user_id
    ),

    KEY idx_poll_responses_poll (poll_id),
    KEY idx_poll_responses_option (option_id),
    KEY idx_poll_responses_user (user_id),

    CONSTRAINT fk_poll_responses_poll
        FOREIGN KEY (poll_id)
        REFERENCES polls(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_poll_responses_option
        FOREIGN KEY (option_id)
        REFERENCES poll_options(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_poll_responses_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 37. AUDIT_LOGS
-- ============================================================

CREATE TABLE audit_logs (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NULL,

    action VARCHAR(150) NOT NULL,

    entity_type VARCHAR(100) NULL,
    entity_id BIGINT UNSIGNED NULL,

    old_values JSON NULL,
    new_values JSON NULL,

    ip_address VARCHAR(45) NULL,
    user_agent VARCHAR(500) NULL,

    description TEXT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_audit_logs_user (user_id),
    KEY idx_audit_logs_action (action),
    KEY idx_audit_logs_entity (
        entity_type,
        entity_id
    ),
    KEY idx_audit_logs_created (created_at),

    CONSTRAINT fk_audit_logs_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 38. EVENT_APPROVALS
-- ============================================================

CREATE TABLE event_approvals (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    event_id BIGINT UNSIGNED NOT NULL,
    reviewer_id BIGINT UNSIGNED NOT NULL,

    action VARCHAR(20) NOT NULL,
    reason TEXT NULL,

    reviewed_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_event_approvals_event (event_id),
    KEY idx_event_approvals_reviewer (reviewer_id),

    CONSTRAINT fk_event_approvals_event
        FOREIGN KEY (event_id)
        REFERENCES events(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_event_approvals_reviewer
        FOREIGN KEY (reviewer_id)
        REFERENCES users(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 39. SYSTEM_SETTINGS
-- ============================================================

CREATE TABLE system_settings (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    setting_key VARCHAR(100) NOT NULL,
    setting_value TEXT NULL,
    description VARCHAR(500) NULL,

    updated_by BIGINT UNSIGNED NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_system_settings_key (setting_key),

    KEY idx_system_settings_updated_by (updated_by),

    CONSTRAINT fk_system_settings_updated_by
        FOREIGN KEY (updated_by)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 40. CONTENT_REPORTS
-- ============================================================

CREATE TABLE content_reports (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    reporter_id BIGINT UNSIGNED NOT NULL,

    content_type VARCHAR(50) NOT NULL,
    content_id BIGINT UNSIGNED NOT NULL,

    reason VARCHAR(150) NOT NULL,
    description TEXT NULL,

    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',

    reviewed_by BIGINT UNSIGNED NULL,
    reviewed_at DATETIME(6) NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_content_reports_reporter (reporter_id),
    KEY idx_content_reports_reviewer (reviewed_by),
    KEY idx_content_reports_status (status),

    CONSTRAINT fk_content_reports_reporter
        FOREIGN KEY (reporter_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_content_reports_reviewer
        FOREIGN KEY (reviewed_by)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 41. USER_SUSPENSIONS
-- ============================================================

CREATE TABLE user_suspensions (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    user_id BIGINT UNSIGNED NOT NULL,
    suspended_by BIGINT UNSIGNED NOT NULL,

    reason TEXT NOT NULL,

    start_date DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    end_date DATETIME(6) NULL,

    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),

    KEY idx_user_suspensions_user (user_id),
    KEY idx_user_suspensions_admin (suspended_by),
    KEY idx_user_suspensions_status (status),

    CONSTRAINT fk_user_suspensions_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_user_suspensions_admin
        FOREIGN KEY (suspended_by)
        REFERENCES users(id)
        ON DELETE RESTRICT
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 42. CLUBS & STUDENT SOCIETIES
-- ============================================================

CREATE TABLE IF NOT EXISTS clubs (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    name VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL,
    short_name VARCHAR(50) NULL,
    description TEXT NULL,
    logo_url VARCHAR(500) NULL,
    cover_image_url VARCHAR(500) NULL,

    faculty_id BIGINT UNSIGNED NULL,
    department_id BIGINT UNSIGNED NULL,
    organization_id BIGINT UNSIGNED NULL,
    president_id BIGINT UNSIGNED NULL,

    status VARCHAR(20) NOT NULL DEFAULT 'ACTIVE',

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
        ON UPDATE CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_clubs_slug (slug),

    KEY idx_clubs_faculty (faculty_id),
    KEY idx_clubs_dept (department_id),
    KEY idx_clubs_org (organization_id),
    KEY idx_clubs_president (president_id),
    KEY idx_clubs_status (status),

    CONSTRAINT fk_clubs_faculty
        FOREIGN KEY (faculty_id)
        REFERENCES faculties(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_clubs_department
        FOREIGN KEY (department_id)
        REFERENCES departments(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_clubs_organization
        FOREIGN KEY (organization_id)
        REFERENCES organizations(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE,

    CONSTRAINT fk_clubs_president
        FOREIGN KEY (president_id)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 43. CLUB_INTERESTS
-- ============================================================

CREATE TABLE IF NOT EXISTS club_interests (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    club_id BIGINT UNSIGNED NOT NULL,
    category_id BIGINT UNSIGNED NOT NULL,

    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_club_category_interest (club_id, category_id),

    KEY idx_club_interests_club (club_id),
    KEY idx_club_interests_category (category_id),

    CONSTRAINT fk_club_interests_club
        FOREIGN KEY (club_id)
        REFERENCES clubs(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_club_interests_category
        FOREIGN KEY (category_id)
        REFERENCES event_categories(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 44. CLUB_FOLLOWERS
-- ============================================================

CREATE TABLE IF NOT EXISTS club_followers (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    club_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    followed_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),

    PRIMARY KEY (id),
    UNIQUE KEY uq_club_user_follower (club_id, user_id),

    KEY idx_club_followers_club (club_id),
    KEY idx_club_followers_user (user_id),

    CONSTRAINT fk_club_followers_club
        FOREIGN KEY (club_id)
        REFERENCES clubs(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_club_followers_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- 45. CLUB_MEMBERS
-- ============================================================

CREATE TABLE IF NOT EXISTS club_members (
    id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,

    club_id BIGINT UNSIGNED NOT NULL,
    user_id BIGINT UNSIGNED NOT NULL,

    membership_role VARCHAR(20) NOT NULL DEFAULT 'MEMBER',
    status VARCHAR(20) NOT NULL DEFAULT 'PENDING',

    applied_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    reviewed_at DATETIME(6) NULL,
    reviewed_by BIGINT UNSIGNED NULL,
    request_notes TEXT NULL,

    PRIMARY KEY (id),
    UNIQUE KEY uq_club_user_membership (club_id, user_id),

    KEY idx_club_members_club (club_id),
    KEY idx_club_members_user (user_id),
    KEY idx_club_members_status (status),

    CONSTRAINT fk_club_members_club
        FOREIGN KEY (club_id)
        REFERENCES clubs(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_club_members_user
        FOREIGN KEY (user_id)
        REFERENCES users(id)
        ON DELETE CASCADE
        ON UPDATE CASCADE,

    CONSTRAINT fk_club_members_reviewer
        FOREIGN KEY (reviewed_by)
        REFERENCES users(id)
        ON DELETE SET NULL
        ON UPDATE CASCADE
) ENGINE=InnoDB;


-- ============================================================
-- SEED INITIAL DATA
-- ============================================================

-- 1. ROLES
INSERT INTO roles (id, name, description, is_system_role) VALUES
(1, 'SuperAdmin', 'Unrestricted Full University Platform Control & Security Governance', TRUE),
(2, 'Admin', 'Campus Events, Venues, and Departmental Operations Administrator', TRUE),
(3, 'Faculty', 'Academic Faculty, Professors, and Course Schedule Coordinators', TRUE),
(4, 'Staff', 'Departmental Staff, Operations Officers, and Equipment Managers', TRUE),
(5, 'Organization', 'Registered Student Clubs, Associations, and Event Organizers', TRUE),
(6, 'Student', 'Enrolled University Students and Event Attendees', TRUE);

-- 2. PERMISSIONS
INSERT INTO permissions (id, name, description, module) VALUES
(1, 'users.manage', 'Create, update, suspend, and manage campus user accounts', 'Users'),
(2, 'users.view', 'View campus directory and public user profiles', 'Users'),
(3, 'events.manage', 'Create, edit, approve, publish, and delete campus events', 'Events'),
(4, 'events.approve', 'Approve or reject submitted student & club events', 'Events'),
(5, 'events.register', 'Register and reserve tickets for campus events', 'Events'),
(6, 'venues.manage', 'Manage campus venues, room capacities, and booking slots', 'Venues'),
(7, 'announcements.publish', 'Publish campus-wide notices and urgent academic alerts', 'Announcements'),
(8, 'system.settings', 'Manage global platform configuration and telemetry settings', 'System'),
(9, 'audit.view', 'View security audit trails and system access logs', 'Security');

-- 3. ROLE_PERMISSIONS
INSERT INTO role_permissions (role_id, permission_id) VALUES
-- SuperAdmin (All permissions)
(1, 1), (1, 2), (1, 3), (1, 4), (1, 5), (1, 6), (1, 7), (1, 8), (1, 9),
-- Admin
(2, 1), (2, 2), (2, 3), (2, 4), (2, 5), (2, 6), (2, 7), (2, 9),
-- Faculty
(3, 2), (3, 3), (3, 5), (3, 7),
-- Staff
(4, 2), (4, 3), (4, 5), (4, 6),
-- Organization
(5, 2), (5, 3), (5, 5),
-- Student
(6, 2), (6, 5);

-- 4. FACULTIES
INSERT INTO faculties (id, name, code, description, dean_name, email, phone, is_active) VALUES
(1, 'Institute of Technology (IoT)', 'IOT', 'Engineering, software, computing, and applied technology disciplines', 'Dr. Ermias Tesfaye', 'iot.dean@hawassa.edu.et', '+251462205311', TRUE),
(2, 'College of Informatics & Computing', 'CIC', 'Computer Science, Information Systems, and Cyber Security', 'Dr. Martha Tadesse', 'informatics@hawassa.edu.et', '+251462205312', TRUE),
(3, 'School of Business & Economics', 'SBE', 'Management, Accounting, Finance, and Marketing Studies', 'Prof. Bekele Gemechu', 'business@hawassa.edu.et', '+251462205313', TRUE),
(4, 'College of Medicine & Health Sciences', 'CMHS', 'Clinical Medicine, Public Health, Nursing, and Medical Tech', 'Dr. Sara Mohammed', 'health@hawassa.edu.et', '+251462205314', TRUE);

-- 5. DEPARTMENTS
INSERT INTO departments (id, faculty_id, name, code, description, head_name, email, phone, is_active) VALUES
(1, 1, 'Software Engineering', 'SE', 'Undergraduate and graduate software design and development', 'Dr. Dawit Alemu', 'se.dept@hawassa.edu.et', '+251462205401', TRUE),
(2, 2, 'Computer Science', 'CS', 'Algorithms, data structures, artificial intelligence, and systems', 'Dr. Abebe Bekele', 'cs.dept@hawassa.edu.et', '+251462205402', TRUE),
(3, 2, 'Information Technology & Cybersecurity', 'IT', 'Network infrastructure, systems administration, and cloud defense', 'Ato Yonas Girma', 'it.dept@hawassa.edu.et', '+251462205403', TRUE),
(4, 1, 'Electrical & Computer Engineering', 'ECE', 'Hardware, microcontrollers, telecommunications, and robotics', 'Dr. Tigist Haile', 'ece.dept@hawassa.edu.et', '+251462205404', TRUE),
(5, 3, 'Accounting & Finance', 'ACCF', 'Financial management, auditing, and business analytics', 'W/ro Selamawit T.', 'accf.dept@hawassa.edu.et', '+251462205405', TRUE);

-- 6. EVENT_CATEGORIES
INSERT INTO event_categories (id, name, slug, description, icon, is_active) VALUES
(1, 'Academic & Technology', 'academic-technology', 'Conferences, AI symposiums, coding hackathons, and lectures', 'bi-laptop', TRUE),
(2, 'Career & Networking', 'career-networking', 'Job fairs, resume clinics, industry panels, and internship recruiting', 'bi-briefcase', TRUE),
(3, 'Sports & Athletics', 'sports-athletics', 'Inter-departmental tournaments, athletics championships, and fitness', 'bi-trophy', TRUE),
(4, 'Arts & Culture', 'arts-culture', 'Cultural gala, music concerts, literature festivals, and exhibitions', 'bi-palette', TRUE),
(5, 'Student Clubs & Workshops', 'clubs-workshops', 'Student union activities, club general assemblies, and skill workshops', 'bi-people', TRUE),
(6, 'Community & Health', 'community-health', 'Blood donation drives, campus wellness, and environmental tree planting', 'bi-heart-pulse', TRUE);

-- 7. EVENT_TAGS
INSERT INTO event_tags (id, name, slug, description) VALUES
(1, 'Coding', 'coding', 'Programming, software development, and algorithm challenges'),
(2, 'AI & Robotics', 'ai-robotics', 'Machine learning, data science, and autonomous systems'),
(3, 'Career Fair', 'career-fair', 'Employer recruiting, internships, and hiring events'),
(4, 'Sports Tournament', 'sports-tournament', 'Football, basketball, and campus athletic games'),
(5, 'Workshop', 'workshop', 'Hands-on practical training and certification sessions'),
(6, 'Cultural Night', 'cultural-night', 'Traditional music, dance, and cultural heritage celebration');

-- 8. VENUES
INSERT INTO venues (id, name, building_name, room_number, description, capacity, venue_type, status) VALUES
(1, 'Hawassa Main Campus Grand Auditorium', 'Administration Complex', 'Auditorium-A', 'Main campus ceremonial and high-capacity keynote hall', 1500, 'AUDITORIUM', 'AVAILABLE'),
(2, 'IoT Tech Hall B', 'Institute of Technology Building', 'Hall-B101', 'Modern amphitheater lecture hall with AV streaming hardware', 350, 'LECTURE_HALL', 'AVAILABLE'),
(3, 'Cybersecurity & Computing Lab 3', 'Informatics Building', 'Lab-304', 'High-performance workstations with gigabit campus LAN', 80, 'LAB', 'AVAILABLE'),
(4, 'Hawassa University Central Stadium', 'Sports Complex', 'Stadium-Main', 'Standard grass pitch, running tracks, and shaded pavilion stands', 5000, 'SPORTS_FIELD', 'AVAILABLE'),
(5, 'Senate Executive Conference Room', 'University Senate Hall', 'Senate-200', 'Executive conference suite for academic symposiums & board meets', 60, 'MEETING_ROOM', 'AVAILABLE');

-- 9. SEED ACCOUNTS: SUPERADMIN AND ADMIN (WITH DISTINCT CREDENTIALS)
-- Master SuperAdmin (Platform Owner): superadmin@hawassa.edu.et / SuperAdmin@2026!
-- Campus Administrator (Events & Operations): admin@hawassa.edu.et / Admin@2026!
INSERT INTO users (id, department_id, username, email, password_hash, first_name, last_name, student_id, employee_id, phone, account_type, account_status, email_verified, phone_verified) VALUES
(1, 2, 'superadmin', 'superadmin@hawassa.edu.et', 'b4a0980c619b02a24c96be11311b70c9c7f66e04d4dd266ec56cb04f9dfc0aa1', 'Dr. Ermias', 'SuperAdmin', NULL, 'EMP-SA-001', '+251911223344', 'SUPERADMIN', 'ACTIVE', TRUE, TRUE),
(2, 1, 'admin', 'admin@hawassa.edu.et', 'b4a0980c619b02a24c96be11311b70c9c7f66e04d4dd266ec56cb04f9dfc0aa1', 'Abebe', 'Administrator', NULL, 'EMP-ADM-002', '+251911556677', 'ADMIN', 'ACTIVE', TRUE, TRUE);

-- 10. ASSIGN ROLES
INSERT INTO user_roles (user_id, role_id, assigned_by) VALUES
(1, 1, 1),  -- User 1 is SuperAdmin (role_id = 1)
(2, 2, 1);  -- User 2 is Admin (role_id = 2)

-- 11. SAMPLE ORGANIZATIONS
INSERT INTO organizations (id, department_id, name, short_name, description, organization_type, email, status) VALUES
(1, 1, 'Google Developer Student Club (GDSC HU)', 'GDSC-HU', 'University chapter for Google developer technologies, Android, and Cloud', 'CLUB', 'gdsc@hawassa.edu.et', 'ACTIVE'),
(2, 2, 'Hawassa Cyber Knights Security Guild', 'CyberKnights', 'Student ethical hacking, cyber defense, and CTF competition guild', 'CLUB', 'cyber@hawassa.edu.et', 'ACTIVE'),
(3, 1, 'Hawassa University Student Union', 'HUSU', 'Official central representative body for Hawassa University students', 'STUDENT_UNION', 'studentunion@hawassa.edu.et', 'ACTIVE');

-- 12. SAMPLE EVENTS
INSERT INTO events (id, title, slug, description, short_description, category_id, organizer_id, organization_id, venue_id, start_at, end_at, capacity, registration_required, event_mode, status, approval_status, is_featured, is_public) VALUES
(1, 'Hawassa National Tech Hackathon 2026', 'hawassa-national-tech-hackathon-2026', '48-hour continuous coding hackathon bringing together software engineers from across Ethiopian universities to build solutions in Fintech, Agriculture, and Healthtech.', '48-hour continuous hackathon for web, mobile, and AI solutions.', 1, 1, 1, 1, DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 5 DAY), DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 7 DAY), 300, TRUE, 'IN_PERSON', 'PUBLISHED', 'APPROVED', TRUE, TRUE),
(2, 'Annual Campus Career & Internship Fair', 'annual-campus-career-fair-2026', 'Meet over 40 technology employers, financial institutions, telecom companies, and NGOs for on-campus interviews and direct internship offers.', 'Annual networking & job recruitment fair with leading companies.', 2, 1, NULL, 2, DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 12 DAY), DATE_ADD(DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 12 DAY), INTERVAL 8 HOUR), 800, TRUE, 'IN_PERSON', 'PUBLISHED', 'APPROVED', TRUE, TRUE),
(3, 'Inter-College Football Championship Cup', 'inter-college-football-championship-2026', 'The annual tournament clash between Institute of Technology, Informatics, Business School, and Health Sciences.', 'Annual inter-department soccer tournament.', 3, 1, 3, 4, DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 18 DAY), DATE_ADD(DATE_ADD(CURRENT_TIMESTAMP, INTERVAL 18 DAY), INTERVAL 4 HOUR), 2000, FALSE, 'IN_PERSON', 'PUBLISHED', 'APPROVED', TRUE, TRUE);

-- 13. SAMPLE ANNOUNCEMENTS
INSERT INTO announcements (id, title, slug, content, summary, author_id, department_id, announcement_type, priority, status, published_at) VALUES
(1, 'Campus-Wide Semester Registration Deadline Announced', 'campus-wide-semester-registration-deadline', 'All undergraduate and postgraduate students must finalize semester module registration through the unified student portal by Friday 5:00 PM.', 'Module registration deadline for the upcoming academic session.', 1, 2, 'ACADEMIC', 'URGENT', 'PUBLISHED', CURRENT_TIMESTAMP(6)),
(2, 'Grand Tech Hackathon 2026 Registrations are Open', 'grand-tech-hackathon-2026-open', 'Registration for teams of 3-4 students is now officially open for the Annual Hawassa National Hackathon.', 'Register your hackathon teams before slots fill up.', 1, 1, 'NEWS', 'HIGH', 'PUBLISHED', CURRENT_TIMESTAMP(6));

-- 14. SAMPLE SYSTEM SETTINGS
INSERT INTO system_settings (setting_key, setting_value, description, updated_by) VALUES
('PortalName', 'Hawassa Unified Campus Event Management System', 'Official portal branding title', 1),
('MaxEventCapacityDefault', '1000', 'Maximum default attendee capacity per single event booking', 1),
('AllowGuestRegistrations', 'false', 'Require verified student/staff institutional credentials for event tickets', 1),
('MaintenanceMode', 'false', 'System maintenance flag', 1);

-- 15. SAMPLE CLUBS & SOCIETIES
INSERT INTO clubs (id, name, slug, short_name, description, logo_url, faculty_id, department_id, organization_id, president_id, status) VALUES
(1, 'AI & Machine Learning Club', 'ai-machine-learning-club', 'AIML', 'Student community dedicated to deep learning, neural networks, PyTorch, computer vision, and applied generative AI engineering.', 'https://images.unsplash.com/photo-1677442136019-21780efad99a?w=400&auto=format&fit=crop&q=80', 2, 2, 1, 1, 'ACTIVE'),
(2, 'Hawassa Cybersecurity Guild', 'hawassa-cybersecurity-guild', 'HCG', 'Hands-on ethical hacking, reverse engineering, CTF team training, network defense, and zero-trust security research.', 'https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=400&auto=format&fit=crop&q=80', 2, 3, 2, 2, 'ACTIVE'),
(3, 'Campus Coding Society', 'campus-coding-society', 'CCS', 'Algorithmic problem solving, competitive programming, full-stack open source software development, and interview prep.', 'https://images.unsplash.com/photo-1555066931-4365d14bab8c?w=400&auto=format&fit=crop&q=80', 1, 1, 1, 1, 'ACTIVE'),
(4, 'Robotics & IoT Society', 'robotics-iot-society', 'RIoT', 'Hardware prototyping, embedded microcontrollers (ESP32, Arduino, Raspberry Pi), drones, and automated smart agricultural sensors.', 'https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=400&auto=format&fit=crop&q=80', 1, 4, 1, 2, 'ACTIVE');

-- 16. CLUB INTERESTS MAPPINGS
INSERT INTO club_interests (club_id, category_id) VALUES
(1, 1), -- AI Club -> Academic & Technology
(1, 5), -- AI Club -> Student Clubs & Workshops
(2, 1), -- Cyber Guild -> Academic & Technology
(2, 2), -- Cyber Guild -> Career & Networking
(3, 1), -- Coding Society -> Academic & Technology
(3, 2), -- Coding Society -> Career & Networking
(3, 5), -- Coding Society -> Student Clubs & Workshops
(4, 1), -- Robotics -> Academic & Technology
(4, 5); -- Robotics -> Student Clubs & Workshops

-- 17. CLUB LEADERSHIP MEMBERS
INSERT INTO club_members (club_id, user_id, membership_role, status, reviewed_at, reviewed_by) VALUES
(1, 1, 'PRESIDENT', 'APPROVED', CURRENT_TIMESTAMP(6), 1),
(2, 2, 'PRESIDENT', 'APPROVED', CURRENT_TIMESTAMP(6), 1),
(3, 1, 'PRESIDENT', 'APPROVED', CURRENT_TIMESTAMP(6), 1),
(4, 2, 'PRESIDENT', 'APPROVED', CURRENT_TIMESTAMP(6), 1);


-- ============================================================
-- FINISH
-- ============================================================

SET FOREIGN_KEY_CHECKS = 1;


-- ============================================================
-- VERIFY DATABASE
-- ============================================================

SELECT
    COUNT(*) AS total_tables
FROM information_schema.tables
WHERE table_schema = 'university_event_management';


-- ============================================================
-- SHOW ALL TABLES
-- ============================================================

SHOW TABLES;