--
-- PostgreSQL database dump
--

\restrict Vh31PaE5k2hqcUbzoclRbHDeUA8Ptst7I5nF6cHhTJPK5I3PjM5XakwpzG6cr1s

-- Dumped from database version 18.3
-- Dumped by pg_dump version 18.3

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: group_state; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.group_state AS ENUM (
    'Y',
    'N'
);


ALTER TYPE public.group_state OWNER TO postgres;

--
-- Name: patent_name; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.patent_name AS ENUM (
    'V',
    'A',
    'SA'
);


ALTER TYPE public.patent_name OWNER TO postgres;

--
-- Name: session_state; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.session_state AS ENUM (
    'Y',
    'N'
);


ALTER TYPE public.session_state OWNER TO postgres;

--
-- Name: state_online; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.state_online AS ENUM (
    'Y',
    'N'
);


ALTER TYPE public.state_online OWNER TO postgres;

--
-- Name: status_log; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.status_log AS ENUM (
    'S',
    'E',
    'T'
);


ALTER TYPE public.status_log OWNER TO postgres;

--
-- Name: status_task; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.status_task AS ENUM (
    'P',
    'IE'
);


ALTER TYPE public.status_task OWNER TO postgres;

--
-- Name: t_ram; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.t_ram AS ENUM (
    'DDR3',
    'DDR4'
);


ALTER TYPE public.t_ram OWNER TO postgres;

--
-- Name: t_storage; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.t_storage AS ENUM (
    'HD',
    'SSD'
);


ALTER TYPE public.t_storage OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: audit_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.audit_logs (
    id_audit_log bigint NOT NULL,
    uuid_audit_log uuid DEFAULT gen_random_uuid() NOT NULL,
    action character varying(30) NOT NULL,
    created_at timestamp(0) with time zone DEFAULT now() NOT NULL,
    ip_address_adm character varying(30) NOT NULL,
    id_user integer NOT NULL
);


ALTER TABLE public.audit_logs OWNER TO postgres;

--
-- Name: audit_logs_id_audit_log_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.audit_logs ALTER COLUMN id_audit_log ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.audit_logs_id_audit_log_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: devices; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.devices (
    id_device integer NOT NULL,
    uuid_device uuid DEFAULT gen_random_uuid() NOT NULL,
    is_online public.state_online NOT NULL,
    last_seen timestamp(0) with time zone DEFAULT now() NOT NULL,
    registered_at timestamp(0) with time zone DEFAULT now() NOT NULL,
    hostname character varying(100) NOT NULL,
    last_ip character varying(30) NOT NULL,
    mac_address character varying(20) NOT NULL,
    os character varying(20) NOT NULL,
    user_os character varying(20) NOT NULL,
    id_info_device integer NOT NULL,
    id_group integer,
    id_user integer
);


ALTER TABLE public.devices OWNER TO postgres;

--
-- Name: devices_id_device_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.devices ALTER COLUMN id_device ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.devices_id_device_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: devices_softwares; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.devices_softwares (
    id_device integer NOT NULL,
    id_software integer NOT NULL
);


ALTER TABLE public.devices_softwares OWNER TO postgres;

--
-- Name: devices_tasks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.devices_tasks (
    id_device integer NOT NULL,
    id_task integer NOT NULL
);


ALTER TABLE public.devices_tasks OWNER TO postgres;

--
-- Name: groups; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.groups (
    id_group integer NOT NULL,
    uuid_group uuid DEFAULT gen_random_uuid() NOT NULL,
    group_name character varying(45) NOT NULL,
    description character varying(100),
    active public.group_state NOT NULL,
    id_user integer
);


ALTER TABLE public.groups OWNER TO postgres;

--
-- Name: groups_id_group_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.groups ALTER COLUMN id_group ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.groups_id_group_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: info_devices; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.info_devices (
    id_info_device integer NOT NULL,
    uuid_info_device uuid DEFAULT gen_random_uuid() NOT NULL,
    cpu character varying(45) NOT NULL,
    gpu character varying(45) NOT NULL,
    ram integer NOT NULL,
    ram_type public.t_ram NOT NULL,
    storage integer NOT NULL,
    storage_type public.t_storage NOT NULL,
    bios character varying(45) NOT NULL
);


ALTER TABLE public.info_devices OWNER TO postgres;

--
-- Name: info_devices_id_info_device_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.info_devices ALTER COLUMN id_info_device ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.info_devices_id_info_device_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: roles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.roles (
    id_role integer NOT NULL,
    uuid_role uuid DEFAULT gen_random_uuid() NOT NULL,
    name public.patent_name NOT NULL
);


ALTER TABLE public.roles OWNER TO postgres;

--
-- Name: roles_id_role_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.roles ALTER COLUMN id_role ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.roles_id_role_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: sessions; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.sessions (
    id_session integer NOT NULL,
    uuid_session uuid DEFAULT gen_random_uuid() NOT NULL,
    token_hash character varying(255) NOT NULL,
    is_active public.session_state NOT NULL,
    login_time timestamp(0) with time zone DEFAULT now() NOT NULL,
    last_seen timestamp(0) with time zone DEFAULT now() NOT NULL,
    expires_at timestamp(0) with time zone NOT NULL,
    ip_address_login character varying(30) NOT NULL,
    id_user integer NOT NULL
);


ALTER TABLE public.sessions OWNER TO postgres;

--
-- Name: sessions_id_session_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.sessions ALTER COLUMN id_session ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.sessions_id_session_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: softwares; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.softwares (
    id_software integer NOT NULL,
    name character varying(120) NOT NULL
);


ALTER TABLE public.softwares OWNER TO postgres;

--
-- Name: softwares_id_software_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.softwares ALTER COLUMN id_software ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.softwares_id_software_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: task_execution_logs; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.task_execution_logs (
    id_job_exe_log bigint CONSTRAINT task_exexution_logs_id_job_exe_log_not_null NOT NULL,
    uuid_job_exe_log uuid DEFAULT gen_random_uuid() NOT NULL,
    action_type character varying(45) NOT NULL,
    status public.status_log NOT NULL,
    source_output_log character varying(255) NOT NULL,
    executed_at timestamp(0) with time zone DEFAULT now() NOT NULL,
    id_task integer NOT NULL,
    id_user integer NOT NULL,
    id_device integer NOT NULL
);


ALTER TABLE public.task_execution_logs OWNER TO postgres;

--
-- Name: task_exexution_logs_id_job_exe_log_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.task_execution_logs ALTER COLUMN id_job_exe_log ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.task_exexution_logs_id_job_exe_log_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: tasks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.tasks (
    id_task integer NOT NULL,
    task_name character varying(45) NOT NULL,
    source_script character varying(255) NOT NULL,
    date_task timestamp(0) with time zone DEFAULT now() NOT NULL,
    status public.status_task NOT NULL
);


ALTER TABLE public.tasks OWNER TO postgres;

--
-- Name: tasks_id_task_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.tasks ALTER COLUMN id_task ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.tasks_id_task_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id_user integer NOT NULL,
    uuid_user uuid DEFAULT gen_random_uuid() NOT NULL,
    username character varying(25) NOT NULL,
    email character varying(60) NOT NULL,
    password_user character varying(255) NOT NULL,
    id_role integer NOT NULL
);


ALTER TABLE public.users OWNER TO postgres;

--
-- Name: users_id_user_seq; Type: SEQUENCE; Schema: public; Owner: postgres
--

ALTER TABLE public.users ALTER COLUMN id_user ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.users_id_user_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);


--
-- Name: audit_logs audit_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT audit_logs_pkey PRIMARY KEY (id_audit_log);


--
-- Name: devices devices_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT devices_pkey PRIMARY KEY (id_device);


--
-- Name: devices_softwares devices_softwares_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_softwares
    ADD CONSTRAINT devices_softwares_pkey PRIMARY KEY (id_device, id_software);


--
-- Name: devices_tasks devices_tasks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_tasks
    ADD CONSTRAINT devices_tasks_pkey PRIMARY KEY (id_device, id_task);


--
-- Name: groups groups_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.groups
    ADD CONSTRAINT groups_pkey PRIMARY KEY (id_group);


--
-- Name: info_devices info_devices_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.info_devices
    ADD CONSTRAINT info_devices_pkey PRIMARY KEY (id_info_device);


--
-- Name: roles roles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.roles
    ADD CONSTRAINT roles_pkey PRIMARY KEY (id_role);


--
-- Name: sessions sessions_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_pkey PRIMARY KEY (id_session);


--
-- Name: softwares softwares_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.softwares
    ADD CONSTRAINT softwares_pkey PRIMARY KEY (id_software);


--
-- Name: task_execution_logs task_exexution_logs_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_execution_logs
    ADD CONSTRAINT task_exexution_logs_pkey PRIMARY KEY (id_job_exe_log);


--
-- Name: tasks tasks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.tasks
    ADD CONSTRAINT tasks_pkey PRIMARY KEY (id_task);


--
-- Name: devices uk_devices_hostname; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT uk_devices_hostname UNIQUE (hostname);


--
-- Name: devices uk_devices_last_ip; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT uk_devices_last_ip UNIQUE (last_ip);


--
-- Name: devices uk_devices_mac_address; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT uk_devices_mac_address UNIQUE (mac_address);


--
-- Name: groups uk_groups_group_name; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.groups
    ADD CONSTRAINT uk_groups_group_name UNIQUE (group_name);


--
-- Name: softwares uk_softwares_name; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.softwares
    ADD CONSTRAINT uk_softwares_name UNIQUE (name);


--
-- Name: users uk_users_email; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT uk_users_email UNIQUE (email);


--
-- Name: users uk_users_username; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT uk_users_username UNIQUE (username);


--
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id_user);


--
-- Name: audit_logs audit_logs_id_user_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.audit_logs
    ADD CONSTRAINT audit_logs_id_user_fkey FOREIGN KEY (id_user) REFERENCES public.users(id_user);


--
-- Name: devices devices_id_group_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT devices_id_group_fkey FOREIGN KEY (id_group) REFERENCES public.groups(id_group);


--
-- Name: devices devices_id_info_device_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT devices_id_info_device_fkey FOREIGN KEY (id_info_device) REFERENCES public.info_devices(id_info_device);


--
-- Name: devices devices_id_user_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices
    ADD CONSTRAINT devices_id_user_fkey FOREIGN KEY (id_user) REFERENCES public.users(id_user);


--
-- Name: devices_softwares devices_softwares_id_device_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_softwares
    ADD CONSTRAINT devices_softwares_id_device_fkey FOREIGN KEY (id_device) REFERENCES public.devices(id_device) ON DELETE CASCADE;


--
-- Name: devices_softwares devices_softwares_id_software_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_softwares
    ADD CONSTRAINT devices_softwares_id_software_fkey FOREIGN KEY (id_software) REFERENCES public.softwares(id_software) ON DELETE CASCADE;


--
-- Name: devices_tasks devices_tasks_id_device_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_tasks
    ADD CONSTRAINT devices_tasks_id_device_fkey FOREIGN KEY (id_device) REFERENCES public.devices(id_device) ON DELETE CASCADE;


--
-- Name: devices_tasks devices_tasks_id_task_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.devices_tasks
    ADD CONSTRAINT devices_tasks_id_task_fkey FOREIGN KEY (id_task) REFERENCES public.tasks(id_task) ON DELETE CASCADE;


--
-- Name: groups groups_id_user_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.groups
    ADD CONSTRAINT groups_id_user_fkey FOREIGN KEY (id_user) REFERENCES public.users(id_user);


--
-- Name: sessions sessions_id_user_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.sessions
    ADD CONSTRAINT sessions_id_user_fkey FOREIGN KEY (id_user) REFERENCES public.users(id_user) ON DELETE CASCADE;


--
-- Name: task_execution_logs task_exexution_logs_id_device_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_execution_logs
    ADD CONSTRAINT task_exexution_logs_id_device_fkey FOREIGN KEY (id_device) REFERENCES public.devices(id_device);


--
-- Name: task_execution_logs task_exexution_logs_id_task_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_execution_logs
    ADD CONSTRAINT task_exexution_logs_id_task_fkey FOREIGN KEY (id_task) REFERENCES public.tasks(id_task);


--
-- Name: task_execution_logs task_exexution_logs_id_user_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_execution_logs
    ADD CONSTRAINT task_exexution_logs_id_user_fkey FOREIGN KEY (id_user) REFERENCES public.users(id_user);


--
-- Name: users users_id_role_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_id_role_fkey FOREIGN KEY (id_role) REFERENCES public.roles(id_role) ON DELETE SET NULL;


--
-- PostgreSQL database dump complete
--

\unrestrict Vh31PaE5k2hqcUbzoclRbHDeUA8Ptst7I5nF6cHhTJPK5I3PjM5XakwpzG6cr1s

