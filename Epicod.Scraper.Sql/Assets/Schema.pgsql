CREATE TABLE public.textnode (
	id int NOT NULL,
	parentid int NULL,
	corpus varchar(50) NOT NULL,
	y int NOT NULL,
	x int NOT NULL,
	name varchar(200) NOT NULL,
	uri varchar(300) NULL,
	CONSTRAINT textnode_pk PRIMARY KEY (id)
);

CREATE TABLE textnodeproperty (
	id serial NOT NULL,
	nodeid int4 NOT NULL,
	"name" varchar NOT NULL,
	value varchar NOT NULL,
	CONSTRAINT textnodeproperty_pk PRIMARY KEY (id)
);
CREATE INDEX textnodeproperty_name_idx ON public.textnodeproperty USING btree (name);
ALTER TABLE public.textnodeproperty ADD CONSTRAINT textnodeproperty_fk FOREIGN KEY (nodeid) REFERENCES textnode(id) ON DELETE CASCADE ON UPDATE CASCADE;
